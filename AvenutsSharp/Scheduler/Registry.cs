using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AventusSharp.Scheduler
{
    /// <summary>
    /// A registry of job schedules.
    /// </summary>
    public class Registry
    {
        private bool _allJobsConfiguredAsNonReentrant;

        internal List<Schedule> Schedules { get; private set; }

        /// <summary>
        /// Default ctor.
        /// </summary>
        public Registry()
        {
            _allJobsConfiguredAsNonReentrant = false;
            Schedules = new List<Schedule>();
        }

        /// <summary>
        /// Sets all jobs in this schedule as non reentrant.
        /// </summary>
        public void NonReentrantAsDefault()
        {
            _allJobsConfiguredAsNonReentrant = true;
            lock (((ICollection)Schedules).SyncRoot)
            {
                foreach (Schedule schedule in Schedules)
                {
                    schedule.NonReentrant();
                }
            }
        }

        /// <summary>
        /// Schedules a new job in the registry.
        /// </summary>
        /// <param name="job">Job to run.</param>
        public Schedule Schedule(Expression<Action> job)
        {
            if (job == null)
            {
                throw new ArgumentNullException("job");
            }

            return Schedule(job, null);
        }

        /// <summary>
        /// Schedules a new job in the registry.
        /// </summary>
        /// <param name="job">Job to run.</param>
        public Schedule Schedule(IJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException("job");
            }

            return Schedule(() => JobManager.GetJobAction(job), null);
        }

        /// <summary>
        /// Schedules a new job in the registry.
        /// </summary>
        /// <typeparam name="T">Job to schedule.</typeparam>
        public Schedule Schedule<T>() where T : IJob
        {
            return Schedule(() => JobManager.GetJobAction<T>(), typeof(T).Name);
        }

        /// <summary>
        /// Schedules a new job in the registry.
        /// </summary>
        /// <param name="job">Factory method creating a IJob instance to run.</param>
        public Schedule Schedule(Func<IJob> job)
        {
            if (job == null)
            {
                throw new ArgumentNullException("job");
            }

            return Schedule(() => JobManager.GetJobAction(job), null);
        }

        /// <summary>
        /// Schedules a new job in the registry.
        /// </summary>
        /// <param name="action">Job to run</param>
        /// <param name="name">Name to identify the job</param>
        public Schedule Schedule(Expression<Action> action, string? name)
        {
            Schedule schedule = new Schedule(action.Compile());

            if (_allJobsConfiguredAsNonReentrant)
            {
                schedule.NonReentrant();
            }

            lock (((ICollection)Schedules).SyncRoot)
            {
                Schedules.Add(schedule);
            }

            if (string.IsNullOrEmpty(name))
            {
                MethodCallExpression methodCallExp = (MethodCallExpression)action.Body;
                schedule.Name = methodCallExp.Method.Name;
            }
            else
            {
                schedule.Name = name;
            }

            return schedule;
        }
    }
}
