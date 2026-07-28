using AventusSharp.Scheduler;
using AventusSharp.Scheduler.Event;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
[NonParallelizable]
public sealed class SchedulerJobFactoryTests
{
    private IJobFactory originalFactory = null!;

    [SetUp]
    public void SetUp()
    {
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
        originalFactory = JobManager.JobFactory;
        FactoryJob.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
        JobManager.JobFactory = originalFactory;
    }

    [Test]
    public void Generic_schedule_uses_the_configured_job_factory()
    {
        var factory = new RecordingFactory();
        JobManager.JobFactory = factory;
        var registry = new Registry();
        registry.Schedule<FactoryJob>();

        JobManager.Initialize(registry);
        Assert.That(FactoryJob.Completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        JobManager.StopAndBlock();

        Assert.Multiple(() =>
        {
            Assert.That(factory.Calls, Is.EqualTo(1));
            Assert.That(FactoryJob.Executions, Is.EqualTo(1));
            Assert.That(FactoryJob.Disposals, Is.EqualTo(1));
        });
    }

    [Test]
    public void Factory_is_called_for_each_generic_job_execution()
    {
        var factory = new RecordingFactory();
        JobManager.JobFactory = factory;
        var registry = new Registry();
        var schedule = registry.Schedule<FactoryJob>();
        schedule.AndThen<FactoryJob>();

        JobManager.Initialize(registry);
        Assert.That(
            SpinWait.SpinUntil(() => Volatile.Read(ref FactoryJob.Executions) == 2,
                TimeSpan.FromSeconds(2)),
            Is.True);
        JobManager.StopAndBlock();

        Assert.Multiple(() =>
        {
            Assert.That(factory.Calls, Is.EqualTo(2));
            Assert.That(factory.Instances.Distinct().Count(), Is.EqualTo(2));
            Assert.That(FactoryJob.Disposals, Is.EqualTo(2));
        });
    }

    [Test]
    public void Null_returned_by_a_job_factory_is_reported_as_a_job_exception()
    {
        JobManager.JobFactory = new NullFactory();
        JobExceptionInfo? failure = null;
        using var completed = new ManualResetEventSlim();
        Action<JobExceptionInfo> onException = info => failure = info;
        Action<JobEndInfo> onEnd = _ => completed.Set();
        JobManager.JobException += onException;
        JobManager.JobEnd += onEnd;
        try
        {
            var registry = new Registry();
            registry.Schedule<FactoryJob>();
            JobManager.Initialize(registry);

            Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(failure?.Exception, Is.TypeOf<InvalidOperationException>());
        }
        finally
        {
            JobManager.JobException -= onException;
            JobManager.JobEnd -= onEnd;
        }
    }

    [Test]
    public void And_then_supports_instance_factory_and_generic_jobs_in_order()
    {
        var calls = new List<string>();
        using var completed = new ManualResetEventSlim();
        var schedule = new Schedule(() => calls.Add("action"))
            .AndThen(new CallbackJob(() => calls.Add("instance")))
            .AndThen(() => new CallbackJob(() => calls.Add("factory")))
            .AndThen<FactoryJob>()
            .AndThen(() =>
            {
                calls.Add("last");
                completed.Set();
            });

        schedule.Execute();
        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        JobManager.StopAndBlock();

        Assert.That(calls, Is.EqualTo(new[] { "action", "instance", "factory", "last" }));
        Assert.That(FactoryJob.Executions, Is.EqualTo(1));
    }

    [Test]
    public void Non_reentrant_as_default_applies_to_existing_and_future_schedules()
    {
        var registry = new Registry();
        var existing = registry.Schedule(() => NoOp());
        registry.NonReentrantAsDefault();
        var future = registry.Schedule(() => NoOp());

        Assert.Multiple(() =>
        {
            Assert.That(existing.Reentrant, Is.Not.Null);
            Assert.That(future.Reentrant, Is.Not.Null);
            Assert.That(ReferenceEquals(existing.Reentrant, future.Reentrant), Is.False);
        });
    }

    [Test]
    public void Child_schedule_created_by_and_every_shares_parent_reentrancy_guard()
    {
        var schedule = new Schedule(() => { }).NonReentrant();

        schedule.ToRunNow().AndEvery(1).Minutes();

        Assert.That(schedule.AdditionalSchedules, Has.Count.EqualTo(1));
        Assert.That(schedule.AdditionalSchedules.Single().Reentrant,
            Is.SameAs(schedule.Reentrant));
    }

    private static void NoOp()
    {
    }

    private sealed class RecordingFactory : IJobFactory
    {
        public int Calls { get; private set; }
        public List<IJob> Instances { get; } = [];

        public IJob GetJobInstance<T>() where T : IJob
        {
            Calls++;
            var instance = Activator.CreateInstance<T>();
            Instances.Add(instance);
            return instance;
        }
    }

    private sealed class NullFactory : IJobFactory
    {
        public IJob GetJobInstance<T>() where T : IJob
        {
            return null!;
        }
    }

    public sealed class FactoryJob : IJob, IDisposable
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

    private sealed class CallbackJob(Action callback) : IJob
    {
        public void Execute()
        {
            callback();
        }
    }
}
