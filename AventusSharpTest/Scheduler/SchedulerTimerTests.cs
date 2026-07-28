using System.Diagnostics;
using AventusSharp.Scheduler;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
[NonParallelizable]
public sealed class SchedulerTimerTests
{
    private static int repeatedExecutions;
    private static ManualResetEventSlim repeatedExecutionReached = new();

    [SetUp]
    public void SetUp()
    {
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
        repeatedExecutionReached.Dispose();
        repeatedExecutionReached = new ManualResetEventSlim();
        repeatedExecutions = 0;
    }

    [TearDown]
    public void TearDown()
    {
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
    }

    [Test]
    public void Run_once_in_executes_once_and_removes_the_schedule()
    {
        var executions = 0;
        using var completed = new ManualResetEventSlim();

        JobManager.AddJob(
            () =>
            {
                Interlocked.Increment(ref executions);
                completed.Set();
            },
            schedule => schedule.ToRunOnceIn(40).Milliseconds(),
            "run-once");

        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(
            SpinWait.SpinUntil(() => !JobManager.HasJob("run-once"), TimeSpan.FromSeconds(2)),
            Is.True);
        Thread.Sleep(100);

        Assert.That(executions, Is.EqualTo(1));
    }

    [Test]
    public void Periodic_schedule_executes_repeatedly()
    {
        var executions = 0;
        using var repeated = new ManualResetEventSlim();

        JobManager.AddJob(
            () =>
            {
                if (Interlocked.Increment(ref executions) >= 3)
                {
                    repeated.Set();
                }
            },
            schedule => schedule.ToRunEvery(35).Milliseconds(),
            "periodic");

        Assert.That(repeated.Wait(TimeSpan.FromSeconds(3)), Is.True);
        JobManager.RemoveJob("periodic");
        var countAfterRemoval = Volatile.Read(ref executions);
        Thread.Sleep(150);

        Assert.That(executions, Is.EqualTo(countAfterRemoval));
    }

    [Test]
    public void Stop_prevents_a_pending_job_from_starting()
    {
        using var executed = new ManualResetEventSlim();

        JobManager.AddJob(
            () => executed.Set(),
            schedule => schedule.ToRunOnceIn(500).Milliseconds(),
            "pending");
        JobManager.Stop();

        Assert.That(executed.Wait(TimeSpan.FromMilliseconds(700)), Is.False);
        Assert.That(JobManager.HasJob("pending"), Is.True);
    }

    [Test]
    public void Start_resumes_a_job_left_pending_by_stop()
    {
        using var executed = new ManualResetEventSlim();

        JobManager.AddJob(
            () => executed.Set(),
            schedule => schedule.ToRunOnceIn(250).Milliseconds(),
            "resumed");
        JobManager.Stop();
        Thread.Sleep(350);

        JobManager.Start();

        Assert.That(executed.Wait(TimeSpan.FromSeconds(2)), Is.True);
    }

    [Test]
    public void Stop_and_block_waits_for_a_running_job()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        JobManager.AddJob(
            () =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(3));
            },
            schedule => schedule.ToRunNow(),
            "blocking");

        Assert.That(entered.Wait(TimeSpan.FromSeconds(2)), Is.True);
        var stopwatch = Stopwatch.StartNew();
        var stopping = Task.Run(JobManager.StopAndBlock);
        Assert.That(stopping.Wait(TimeSpan.FromMilliseconds(150)), Is.False);

        release.Set();
        Assert.That(stopping.Wait(TimeSpan.FromSeconds(2)), Is.True);
        stopwatch.Stop();

        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(140)));
        Assert.That(JobManager.RunningSchedules, Is.Empty);
    }

    [Test]
    public void Dynamic_immediate_job_executes_without_a_registry()
    {
        using var completed = new ManualResetEventSlim();

        JobManager.AddJob(
            () => completed.Set(),
            schedule => schedule.ToRunNow(),
            "dynamic-now");

        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
    }

    [Test]
    public void To_run_now_and_every_runs_immediately_then_periodically()
    {
        var registry = new Registry();
        var schedule = registry.Schedule(() => CountRepeatedExecution());
        schedule.ToRunNow().AndEvery(40).Milliseconds();

        JobManager.Initialize(registry);

        Assert.That(repeatedExecutionReached.Wait(TimeSpan.FromSeconds(3)), Is.True);
        Assert.That(repeatedExecutions, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void Running_schedules_contains_an_active_job_only_while_it_runs()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var schedule = new Schedule(() =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(3));
        }, "visible-running");

        schedule.Execute();
        Assert.That(entered.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(JobManager.RunningSchedules, Does.Contain(schedule));

        release.Set();
        JobManager.StopAndBlock();

        Assert.That(JobManager.RunningSchedules, Is.Empty);
    }

    private static void CountRepeatedExecution()
    {
        if (Interlocked.Increment(ref repeatedExecutions) >= 2)
        {
            repeatedExecutionReached.Set();
        }
    }
}
