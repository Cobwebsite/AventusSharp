using AventusSharp.Scheduler;
using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
[NonParallelizable]
public sealed class SchedulerManagerTests
{
    [SetUp]
    public void SetUp()
    {
        SchedulerManager.Stop();
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
        ManagedTask.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ManagedTask.Release.Set();
        SchedulerManager.Stop();
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
    }

    [Test]
    public async Task Manager_discovers_starts_executes_and_reports_schedulables()
    {
        SchedulerTaskErrorInfo? reported = null;
        SchedulerManager.Configure(config => config.OnError = info => reported = info);

        VoidWithError init = await SchedulerManager.Init(typeof(ManagedTask).Assembly);

        Assert.Multiple(() =>
        {
            Assert.That(init.Success, Is.True);
            Assert.That(ManagedTask.Executions, Is.EqualTo(1));
            Assert.That(SchedulerManager.All.Any(x => x is ManagedTask), Is.True);
            Assert.That(JobManager.HasJob(typeof(ManagedTask).FullName!), Is.True);
        });

        ManagedTask.ReturnError = true;
        VoidWithError execution = await SchedulerManager.Exec<ManagedTask>();

        Assert.Multiple(() =>
        {
            Assert.That(execution.Success, Is.False);
            Assert.That(reported?.SchedulableType, Is.EqualTo(typeof(ManagedTask)));
            Assert.That(reported?.Errors.Single().Message, Is.EqualTo("expected"));
        });
    }

    [Test]
    public async Task Manager_prevents_concurrent_execution_of_the_same_type()
    {
        SchedulerManager.Configure(config => config.OnError = _ => { });
        await SchedulerManager.Init(typeof(ManagedTask).Assembly);
        ManagedTask.Block = true;

        Task<VoidWithError> first = SchedulerManager.Exec<ManagedTask>();
        Assert.That(ManagedTask.Entered.Wait(TimeSpan.FromSeconds(2)), Is.True);

        VoidWithError second = await SchedulerManager.Exec<ManagedTask>();
        ManagedTask.Release.Set();
        await first;

        Assert.Multiple(() =>
        {
            Assert.That(second.Success, Is.False);
            Assert.That(second.Errors.Single(), Is.TypeOf<SchedulerError>());
            Assert.That(
                ((SchedulerError)second.Errors.Single()).Code,
                Is.EqualTo(SchedulerErrorCode.SchedulableAlreadyRunning));
            Assert.That(ManagedTask.Executions, Is.EqualTo(2));
        });
    }

    public sealed class ManagedTask : Schedulable
    {
        public static int Executions;
        public static bool ReturnError;
        public static bool Block;
        public static ManualResetEventSlim Entered { get; private set; } = new();
        public static ManualResetEventSlim Release { get; private set; } = new();

        public static void Reset()
        {
            Entered.Dispose();
            Release.Dispose();
            Entered = new ManualResetEventSlim();
            Release = new ManualResetEventSlim();
            Executions = 0;
            ReturnError = false;
            Block = false;
        }

        public override bool TriggerOnStart() => true;

        public override void Schedule(Schedule schedule) =>
            schedule.ToRunEvery(1).Hours();

        protected override async Task<VoidWithError> Run()
        {
            Interlocked.Increment(ref Executions);
            if (Block)
            {
                Entered.Set();
                await Task.Run(() => Release.Wait(TimeSpan.FromSeconds(2)));
            }

            return ReturnError
                ? new VoidWithError { Errors = [new GenericError(500, "expected")] }
                : new VoidWithError();
        }
    }
}
