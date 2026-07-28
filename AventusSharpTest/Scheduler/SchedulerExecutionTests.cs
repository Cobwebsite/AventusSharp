using AventusSharp.Scheduler;
using AventusSharp.Scheduler.Event;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
[NonParallelizable]
public sealed class SchedulerExecutionTests
{
    [SetUp]
    public void SetUp()
    {
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
        CountingJob.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
    }

    [Test]
    public void Execute_raises_start_and_end_events_with_the_schedule_name()
    {
        JobStartInfo? start = null;
        JobEndInfo? end = null;
        using var completed = new ManualResetEventSlim();
        Action<JobStartInfo> onStart = info => start = info;
        Action<JobEndInfo> onEnd = info =>
        {
            end = info;
            completed.Set();
        };

        JobManager.JobStart += onStart;
        JobManager.JobEnd += onEnd;
        try
        {
            new Schedule(() => { }, "event-job").Execute();

            Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(start?.Name, Is.EqualTo("event-job"));
                Assert.That(end?.Name, Is.EqualTo("event-job"));
                Assert.That(end?.StartTime, Is.EqualTo(start?.StartTime));
                Assert.That(end?.Duration, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
            });
        }
        finally
        {
            JobManager.JobStart -= onStart;
            JobManager.JobEnd -= onEnd;
        }
    }

    [Test]
    public void Job_exception_is_reported_and_does_not_prevent_the_end_event()
    {
        JobExceptionInfo? failure = null;
        using var ended = new ManualResetEventSlim();
        Action<JobExceptionInfo> onException = info => failure = info;
        Action<JobEndInfo> onEnd = _ => ended.Set();

        JobManager.JobException += onException;
        JobManager.JobEnd += onEnd;
        try
        {
            new Schedule(() => throw new InvalidOperationException("expected"), "failing-job").Execute();

            Assert.That(ended.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(failure?.Name, Is.EqualTo("failing-job"));
                Assert.That(failure?.Exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(failure?.Exception?.Message, Is.EqualTo("expected"));
            });
        }
        finally
        {
            JobManager.JobException -= onException;
            JobManager.JobEnd -= onEnd;
        }
    }

    [Test]
    public void Disabled_schedule_does_not_execute_until_enabled()
    {
        var executions = 0;
        using var completed = new ManualResetEventSlim();
        var schedule = new Schedule(() =>
        {
            Interlocked.Increment(ref executions);
            completed.Set();
        });

        schedule.Disable();
        schedule.Execute();
        Assert.That(completed.Wait(TimeSpan.FromMilliseconds(150)), Is.False);

        schedule.Enable();
        schedule.Execute();
        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(executions, Is.EqualTo(1));
    }

    [Test]
    public void Non_reentrant_schedule_ignores_a_second_execution_while_running()
    {
        var executions = 0;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var schedule = new Schedule(() =>
        {
            Interlocked.Increment(ref executions);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(2));
        }).NonReentrant();

        schedule.Execute();
        Assert.That(entered.Wait(TimeSpan.FromSeconds(2)), Is.True);
        schedule.Execute();
        Thread.Sleep(100);
        release.Set();
        JobManager.StopAndBlock();

        Assert.That(executions, Is.EqualTo(1));
    }

    [Test]
    public void Registry_executes_an_IJob_instance_and_disposes_it()
    {
        using var completed = new ManualResetEventSlim();
        var job = new DisposableJob(completed);
        var registry = new Registry();
        registry.Schedule(job);

        JobManager.Initialize(registry);
        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        JobManager.StopAndBlock();

        Assert.Multiple(() =>
        {
            Assert.That(job.Executions, Is.EqualTo(1));
            Assert.That(job.Disposed, Is.True);
        });
    }

    [Test]
    public void Registry_executes_a_job_returned_by_a_factory()
    {
        using var completed = new ManualResetEventSlim();
        var job = new DisposableJob(completed);
        var registry = new Registry();
        registry.Schedule(() => job);

        JobManager.Initialize(registry);
        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        JobManager.StopAndBlock();

        Assert.That(job.Executions, Is.EqualTo(1));
        Assert.That(job.Disposed, Is.True);
    }

    [Test]
    public void Registry_generic_job_uses_the_default_factory_and_type_name()
    {
        var registry = new Registry();
        var schedule = registry.Schedule<CountingJob>();

        JobManager.Initialize(registry);
        Assert.That(CountingJob.Completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        JobManager.StopAndBlock();

        Assert.Multiple(() =>
        {
            Assert.That(schedule.Name, Is.EqualTo(nameof(CountingJob)));
            Assert.That(CountingJob.Executions, Is.EqualTo(1));
            Assert.That(CountingJob.Disposals, Is.EqualTo(1));
        });
    }

    [Test]
    public void Remove_job_updates_lookup_and_all_schedules()
    {
        JobManager.AddJob(() => { }, schedule =>
            schedule.ToRunEvery(1).Hours(), "removable");

        Assert.That(JobManager.HasJob("removable"), Is.True);
        JobManager.RemoveJob("removable");

        Assert.Multiple(() =>
        {
            Assert.That(JobManager.HasJob("removable"), Is.False);
            Assert.That(JobManager.GetSchedule("removable"), Is.Null);
            Assert.That(JobManager.AllSchedules, Is.Empty);
        });
    }

    [Test]
    public void Failing_start_subscriber_does_not_prevent_job_or_other_subscribers()
    {
        var executed = 0;
        using var startObserved = new ManualResetEventSlim();
        using var ended = new ManualResetEventSlim();
        Action<JobStartInfo> failing = _ =>
            throw new InvalidOperationException("start observer failure");
        Action<JobStartInfo> succeeding = _ => startObserved.Set();
        Action<JobEndInfo> onEnd = _ => ended.Set();
        JobManager.JobStart += failing;
        JobManager.JobStart += succeeding;
        JobManager.JobEnd += onEnd;
        try
        {
            new Schedule(() => Interlocked.Increment(ref executed)).Execute();

            Assert.That(ended.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(startObserved.IsSet, Is.True);
                Assert.That(executed, Is.EqualTo(1));
                Assert.That(JobManager.RunningSchedules, Is.Empty);
            });
        }
        finally
        {
            JobManager.JobStart -= failing;
            JobManager.JobStart -= succeeding;
            JobManager.JobEnd -= onEnd;
        }
    }

    [Test]
    public void Failing_end_subscriber_does_not_prevent_other_subscribers_or_cleanup()
    {
        using var endObserved = new ManualResetEventSlim();
        Action<JobEndInfo> failing = _ =>
            throw new InvalidOperationException("end observer failure");
        Action<JobEndInfo> succeeding = _ => endObserved.Set();
        JobManager.JobEnd += failing;
        JobManager.JobEnd += succeeding;
        try
        {
            new Schedule(() => { }).Execute();

            Assert.That(endObserved.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(JobManager.RunningSchedules, Is.Empty);
        }
        finally
        {
            JobManager.JobEnd -= failing;
            JobManager.JobEnd -= succeeding;
        }
    }

    [Test]
    public void Failing_exception_subscriber_does_not_hide_failure_from_other_subscribers()
    {
        JobExceptionInfo? observed = null;
        using var exceptionObserved = new ManualResetEventSlim();
        using var ended = new ManualResetEventSlim();
        Action<JobExceptionInfo> failing = _ =>
            throw new InvalidOperationException("exception observer failure");
        Action<JobExceptionInfo> succeeding = info =>
        {
            observed = info;
            exceptionObserved.Set();
        };
        Action<JobEndInfo> onEnd = _ => ended.Set();
        JobManager.JobException += failing;
        JobManager.JobException += succeeding;
        JobManager.JobEnd += onEnd;
        try
        {
            new Schedule(
                () => throw new InvalidOperationException("job failure"),
                "observed-failure").Execute();

            Assert.That(ended.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(exceptionObserved.IsSet, Is.True);
                Assert.That(observed?.Name, Is.EqualTo("observed-failure"));
                Assert.That(observed?.Exception?.Message, Is.EqualTo("job failure"));
                Assert.That(JobManager.RunningSchedules, Is.Empty);
            });
        }
        finally
        {
            JobManager.JobException -= failing;
            JobManager.JobException -= succeeding;
            JobManager.JobEnd -= onEnd;
        }
    }

    private sealed class DisposableJob(ManualResetEventSlim completed) : IJob, IDisposable
    {
        public int Executions { get; private set; }
        public bool Disposed { get; private set; }

        public void Execute()
        {
            Executions++;
            completed.Set();
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    public sealed class CountingJob : IJob, IDisposable
    {
        public static int Executions;
        public static int Disposals;
        public static ManualResetEventSlim Completed { get; private set; } = new();

        public static void Reset()
        {
            Completed.Dispose();
            Completed = new ManualResetEventSlim();
            Executions = 0;
            Disposals = 0;
        }

        public void Execute()
        {
            Interlocked.Increment(ref Executions);
            Completed.Set();
        }

        public void Dispose()
        {
            Interlocked.Increment(ref Disposals);
        }
    }
}
