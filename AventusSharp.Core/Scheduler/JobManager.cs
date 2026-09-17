using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AventusSharp.Scheduler.Event;
using AventusSharp.Scheduler.Util;
using AventusSharp.Tools;
using Microsoft.Extensions.Logging;

namespace AventusSharp.Scheduler
{
    /// <summary>
    /// Job manager that handles jobs execution.
    /// </summary>
    public static class JobManager
    {
        #region Internal fields

        private const uint _maxTimerInterval = 0xfffffffe;

        private static TimeZoneInfo _timeZone = TimeZoneInfo.Local;

        private static readonly Timer _timer = new Timer(state => ScheduleJobs(), null, Timeout.Infinite, Timeout.Infinite);

        private static readonly ScheduleCollection _schedules = new ScheduleCollection();

        private static readonly ISet<Tuple<Schedule, Task>> _running = new HashSet<Tuple<Schedule, Task>>();

        internal static DateTime Now =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

        internal static DateTime GetNow(Schedule schedule) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTimeZone(schedule));

        private static TimeZoneInfo GetTimeZone(Schedule schedule) =>
            schedule.ScheduledTimeZone ?? schedule.Parent?.ScheduledTimeZone ?? _timeZone;

        private static void SetNextRun(Schedule schedule, DateTime nextRun)
        {
            TimeZoneInfo timeZone = GetTimeZone(schedule);
            DateTime localTime = NormalizeLocalTime(nextRun, timeZone);

            schedule.NextRun = localTime;
            schedule.NextRunUtc = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
        }

        private static DateTime ToUtc(Schedule schedule, DateTime dateTime)
        {
            TimeZoneInfo timeZone = GetTimeZone(schedule);
            return TimeZoneInfo.ConvertTimeToUtc(NormalizeLocalTime(dateTime, timeZone), timeZone);
        }

        private static DateTime NormalizeLocalTime(DateTime dateTime, TimeZoneInfo timeZone)
        {
            DateTime localTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);

            // A wall-clock time can be absent during the daylight-saving gap.
            // Move it to the first valid minute so the occurrence is not lost.
            while (timeZone.IsInvalidTime(localTime))
            {
                localTime = localTime.AddMinutes(1);
            }
            return localTime;
        }

        #endregion

        #region Time zone

        /// <summary>
        /// Uses the specified time zone to calculate scheduler occurrences.
        /// Call this method before registering jobs to avoid mixed dates.
        /// </summary>
        /// <param name="timeZone">Time zone used by the scheduler.</param>
        public static void UseTimeZone(TimeZoneInfo timeZone)
        {
            ArgumentNullException.ThrowIfNull(timeZone);
            _timeZone = timeZone;
        }

        #endregion

        #region Job factory

        private static IJobFactory? _jobFactory;

        /// <summary>
        /// Job factory used by the job manager.
        /// </summary>
        public static IJobFactory JobFactory
        {
            get => _jobFactory = _jobFactory ?? new JobFactory();
            set => _jobFactory = value;
        }

        internal static Action GetJobAction<T>() where T : IJob
        {
            return () =>
            {
                IJob job = JobFactory.GetJobInstance<T>();
                if (job == null)
                {
                    throw new InvalidOperationException("The configured IJobFactory returned null.");
                }

                try
                {
                    job.Execute();
                }
                finally
                {
                    DisposeIfNeeded(job);
                }
            };
        }

        internal static Action GetJobAction(IJob job)
        {
            return () =>
            {
                try
                {
                    job.Execute();
                }
                finally
                {
                    DisposeIfNeeded(job);
                }
            };
        }

        internal static Action GetJobAction(Func<IJob> jobFactory)
        {
            return () =>
            {
                IJob job = jobFactory();

                if (job == null)
                {
                    throw new InvalidOperationException("The given Func<IJob> returned null.");
                }

                try
                {
                    job.Execute();
                }
                finally
                {
                    DisposeIfNeeded(job);
                }
            };
        }

        private static void DisposeIfNeeded(IJob job)
        {
            if (job is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        #endregion

        #region Event handling

        /// <summary>
        /// Event raised when an exception occurs in a job.
        /// </summary>
        public static event Action<JobExceptionInfo>? JobException;

        /// <summary>
        /// Event raised when a job starts.
        /// </summary>
        public static event Action<JobStartInfo>? JobStart;

        /// <summary>
        /// Evemt raised when a job ends.
        /// </summary>
        public static event Action<JobEndInfo>? JobEnd;

        #endregion

        #region Start, stop & initialize

        /// <summary>
        /// Initializes the job manager with the jobs to run and starts it.
        /// </summary>
        /// <param name="registries">Registries of jobs to run</param>
        public static void Initialize(params Registry[] registries)
        {
            if (registries == null)
            {
                throw new ArgumentNullException("registries");
            }

            CalculateNextRun(registries.SelectMany(r => r.Schedules)).ToList().ForEach(RunJob);
            Start();
        }

        /// <summary>
        /// Starts the job manager.
        /// </summary>
        public static void Start()
        {
            ScheduleJobs();
        }

        /// <summary>
        /// Stops the job manager.
        /// </summary>
        public static void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Stops the job manager and blocks until all running schedules finishes.
        /// </summary>
        public static void StopAndBlock()
        {
            Stop();

            Task[] tasks = new Task[0];

            // Even though Stop() was just called, a scheduling may be happening right now, that's why the loop.
            // Simply waiting for the tasks inside the lock causes a deadlock (a task may try to remove itself from
            // running, but it can't access the collection, it's blocked by the wait).
            do
            {
                lock (_running)
                {
                    tasks = _running.Select(t => t.Item2).ToArray();
                }

                Task.WaitAll(tasks);
            } while (tasks.Any());
        }

        #endregion

        #region Exposing schedules

        /// <summary>
        /// Returns the schedule of the given name.
        /// </summary>
        /// <param name="name">Name of the schedule.</param>
        /// <returns>The schedule of the given name, if any.</returns>
        public static Schedule? GetSchedule(string name)
        {
            return _schedules.Get(name);
        }

        /// <summary>
        /// Collection of the currently running schedules.
        /// </summary>
        public static IEnumerable<Schedule> RunningSchedules
        {
            get
            {
                lock (_running)
                {
                    return _running.Select(t => t.Item1).ToList();
                }
            }
        }

        /// <summary>
        /// Collection of all schedules.
        /// </summary>
        public static IEnumerable<Schedule> AllSchedules =>
                // returning a shallow copy
                _schedules.All().ToList();

        #endregion

        #region Exposing adding & removing jobs (without the registry)

        /// <summary>
        /// Adds a job schedule to the job manager.
        /// </summary>
        /// <param name="job">Job to run.</param>
        /// <param name="schedule">Job schedule to add.</param>
        public static void AddJob(Action job, Action<Schedule> schedule)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            AddJob(schedule, new Schedule(job));
        }

        /// <summary>
        /// Adds a job schedule to the job manager.
        /// </summary>
        /// <param name="job">Job to run.</param>
        /// <param name="schedule">Job schedule to add.</param>
        /// <param name="name">Job name.</param>
        public static void AddJob(Action job, Action<Schedule> schedule, string name)
        {
            AddJobAndGetSchedule(job, schedule, name);
        }

        internal static Schedule AddJobAndGetSchedule(
            Action job,
            Action<Schedule> schedule,
            string name)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            var registeredSchedule = new Schedule(job, name);
            AddJob(schedule, registeredSchedule);
            return registeredSchedule;
        }

        /// <summary>
        /// Adds a job schedule to the job manager.
        /// </summary>
        /// <param name="job">Job to run.</param>
        /// <param name="schedule">Job schedule to add.</param>
        public static void AddJob(IJob job, Action<Schedule> schedule)
        {
            if (job == null)
            {
                throw new ArgumentNullException("job");
            }

            if (schedule == null)
            {
                throw new ArgumentNullException("schedule");
            }

            AddJob(schedule, new Schedule(GetJobAction(job)));
        }

        /// <summary>
        /// Adds a job schedule to the job manager.
        /// </summary>
        /// <typeparam name="T">Job to run.</typeparam>
        /// <param name="schedule">Job schedule to add.</param>
        public static void AddJob<T>(Action<Schedule> schedule) where T : IJob
        {
            if (schedule == null)
            {
                throw new ArgumentNullException("schedule");
            }

            AddJob(schedule, new Schedule(GetJobAction<T>()) { Name = typeof(T).Name });
        }

        private static void AddJob(Action<Schedule> jobSchedule, Schedule schedule)
        {
            jobSchedule(schedule);
            CalculateNextRun(new Schedule[] { schedule }).ToList().ForEach(RunJob);
            ScheduleJobs();
        }


        /// <summary>
        /// Determine if the job exists
        /// </summary>
        /// <param name="name">Name of the schedule.</param>
        public static bool HasJob(string name)
        {
            return _schedules.Has(name);
        }

        /// <summary>
        /// Removes the schedule of the given name.
        /// </summary>
        /// <param name="name">Name of the schedule.</param>
        public static void RemoveJob(string name)
        {
            _schedules.Remove(name);
        }

        /// <summary>
        /// Removes all schedules.
        /// </summary>
        public static void RemoveAllJobs()
        {
            _schedules.RemoveAll();
        }

        #endregion

        #region Calculating, scheduling & running
        public static void CalculateNextRun(Schedule schedule)
        {
            DateTime now = GetNow(schedule);
            if (schedule.CalculateNextRun == null)
            {
                if (schedule.DelayRunFor > TimeSpan.Zero)
                {
                    // delayed job
                    SetNextRun(schedule, now.Add(schedule.DelayRunFor));
                    _schedules.Add(schedule);
                }
                else
                {
                    return;
                }
                bool hasAdded = false;
                foreach (Schedule child in schedule.AdditionalSchedules.Where(x => x.CalculateNextRun != null))
                {
                    if (child.CalculateNextRun != null)
                    {
                        DateTime childNow = GetNow(child);
                        DateTime nextRun = child.CalculateNextRun(childNow.Add(child.DelayRunFor).AddMilliseconds(1));
                        DateTime nextRunUtc = ToUtc(child, nextRun);
                        if (!hasAdded || schedule.NextRunUtc > nextRunUtc)
                        {
                            SetNextRun(schedule, TimeZoneInfo.ConvertTimeFromUtc(nextRunUtc, GetTimeZone(schedule)));
                            hasAdded = true;
                        }
                    }
                }
            }
            else
            {
                SetNextRun(schedule, schedule.CalculateNextRun(now.Add(schedule.DelayRunFor)));
                _schedules.Add(schedule);
            }
        }


        private static IEnumerable<Schedule> CalculateNextRun(IEnumerable<Schedule> schedules)
        {
            foreach (Schedule schedule in schedules)
            {
                DateTime now = GetNow(schedule);
                if (schedule.CalculateNextRun == null)
                {
                    if (schedule.DelayRunFor > TimeSpan.Zero)
                    {
                        // delayed job
                        SetNextRun(schedule, now.Add(schedule.DelayRunFor));
                        _schedules.Add(schedule);
                    }
                    else
                    {
                        // run immediately
                        yield return schedule;
                    }
                    bool hasAdded = false;
                    foreach (Schedule child in schedule.AdditionalSchedules.Where(x => x.CalculateNextRun != null))
                    {
                        if (child.CalculateNextRun != null)
                        {
                            DateTime childNow = GetNow(child);
                            DateTime nextRun = child.CalculateNextRun(childNow.Add(child.DelayRunFor).AddMilliseconds(1));
                            DateTime nextRunUtc = ToUtc(child, nextRun);
                            if (!hasAdded || schedule.NextRunUtc > nextRunUtc)
                            {
                                SetNextRun(schedule, TimeZoneInfo.ConvertTimeFromUtc(nextRunUtc, GetTimeZone(schedule)));
                                hasAdded = true;
                            }
                        }
                    }
                }
                else
                {
                    SetNextRun(schedule, schedule.CalculateNextRun(now.Add(schedule.DelayRunFor)));
                    _schedules.Add(schedule);
                }

                foreach (Schedule childSchedule in schedule.AdditionalSchedules)
                {
                    DateTime childNow = GetNow(childSchedule);
                    if (childSchedule.CalculateNextRun == null)
                    {
                        if (childSchedule.DelayRunFor > TimeSpan.Zero)
                        {
                            // delayed job
                            SetNextRun(childSchedule, childNow.Add(childSchedule.DelayRunFor));
                            _schedules.Add(childSchedule);
                        }
                        else
                        {
                            // run immediately
                            yield return childSchedule;
                            continue;
                        }
                    }
                    else
                    {
                        SetNextRun(childSchedule, childSchedule.CalculateNextRun(childNow.Add(childSchedule.DelayRunFor)));
                        _schedules.Add(childSchedule);
                    }
                }
            }
        }

        private static void ScheduleJobs()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _schedules.Sort();

            if (!_schedules.Any())
            {
                return;
            }

            Schedule? firstJob = _schedules.First();
            if (firstJob == null) return;

            DateTime utcNow = DateTime.UtcNow;
            if (firstJob.NextRunUtc <= utcNow)
            {
                RunJob(firstJob);
                if (firstJob.CalculateNextRun == null)
                {
                    // probably a ToRunNow().DelayFor() job, there's no CalculateNextRun
                }
                else
                {
                    DateTime jobNow = GetNow(firstJob);
                    SetNextRun(firstJob, firstJob.CalculateNextRun(jobNow.AddMilliseconds(1)));
                }

                if (firstJob.NextRunUtc <= DateTime.UtcNow || firstJob.PendingRunOnce)
                {
                    _schedules.Remove(firstJob);
                }

                firstJob.PendingRunOnce = false;
                ScheduleJobs();
                return;
            }

            TimeSpan interval = firstJob.NextRunUtc - DateTime.UtcNow;

            if (interval <= TimeSpan.Zero)
            {
                ScheduleJobs();
                return;
            }
            else
            {
                if (interval.TotalMilliseconds > _maxTimerInterval)
                {
                    interval = TimeSpan.FromMilliseconds(_maxTimerInterval);
                }

                _timer.Change(interval, interval);
            }
        }

        internal static void RunJob(Schedule schedule)
        {
            if (schedule.Disabled)
            {
                return;
            }

            lock (_running)
            {
                if (schedule.Reentrant != null &&
                    _running.Any(t => ReferenceEquals(t.Item1.Reentrant, schedule.Reentrant)))
                {
                    return;
                }
            }

            Tuple<Schedule, Task>? tuple = null;

            Task task = new Task(() =>
            {
                DateTime start = Now;

                InvokeHandlers(
                    JobStart,
                    new JobStartInfo
                    {
                        Name = schedule.Name,
                        StartTime = start,
                    }
                );

                Stopwatch stopwatch = new Stopwatch();

                try
                {
                    stopwatch.Start();
                    schedule.Jobs.ForEach(action => Task.Factory.StartNew(action).Wait());
                }
                catch (Exception e)
                {
                    if (JobException != null)
                    {
                        if (e is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
                        {
                            e = aggregate.InnerExceptions.Single();
                        }

                        InvokeHandlers(
                           JobException,
                           new JobExceptionInfo
                           {
                               Name = schedule.Name,
                               Exception = e,
                           }
                       );
                    }
                }
                finally
                {
                    lock (_running)
                    {
                        if (tuple != null)
                            _running.Remove(tuple);
                    }

                    InvokeHandlers(
                        JobEnd,
                        new JobEndInfo
                        {
                            Name = schedule.Name,
                            StartTime = start,
                            Duration = stopwatch.Elapsed,
                            NextRun = schedule.NextRun,
                        }
                    );
                }
            }, TaskCreationOptions.PreferFairness);

            tuple = new Tuple<Schedule, Task>(schedule, task);

            lock (_running)
            {
                _running.Add(tuple);
            }

            task.Start();
        }

        private static void InvokeHandlers<T>(
            Action<T>? handlers,
            T eventInfo)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(eventInfo);
                }
                catch (Exception exception)
                {
                    AventusLogger.Instance.LogError(
                        exception,
                        $"A scheduler {typeof(T).Name} handler failed");
                }
            }
        }

        #endregion
    }
}
