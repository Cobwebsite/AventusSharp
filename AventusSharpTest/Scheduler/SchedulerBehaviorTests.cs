using AventusSharp.Scheduler;
using AventusSharp.Scheduler.Cron;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
[NonParallelizable]
public sealed class SchedulerBehaviorTests
{
    [TearDown]
    public void ClearJobs()
    {
        JobManager.Stop();
        JobManager.RemoveAllJobs();
    }

    [Test]
    public void Crontab_field_supports_ranges_lists_steps_and_names()
    {
        var minutes = CrontabField.Minutes("1-5/2,10");
        var months = CrontabField.Months("January,mar");

        Assert.Multiple(() =>
        {
            Assert.That(minutes.Contains(1), Is.True);
            Assert.That(minutes.Contains(3), Is.True);
            Assert.That(minutes.Contains(5), Is.True);
            Assert.That(minutes.Contains(10), Is.True);
            Assert.That(minutes.Contains(2), Is.False);
            Assert.That(months.Contains(1), Is.True);
            Assert.That(months.Contains(3), Is.True);
        });
    }

    [Test]
    public void Invalid_crontab_fields_are_rejected()
    {
        Assert.Throws<CrontabException>(() => CrontabField.Hours("24"));
        Assert.That(CrontabField.TryParse(CrontabFieldKind.Minute, "invalid"), Is.Null);
    }

    [Test]
    public void Cron_calculator_returns_next_occurrence()
    {
        var calculator = new CronTimeCalculator("0 */5 * * * *");
        var last = new DateTime(2026, 7, 24, 10, 2, 30);

        var next = calculator.Calculate(last);

        Assert.That(next, Is.EqualTo(new DateTime(2026, 7, 24, 10, 5, 0)));
    }

    [Test]
    public void Schedule_executes_chained_actions_in_order()
    {
        var calls = new List<int>();
        using var completed = new ManualResetEventSlim();
        var schedule = new Schedule(() => calls.Add(1))
            .AndThen(() =>
            {
                calls.Add(2);
                completed.Set();
            });

        schedule.Execute();
        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);

        Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void Registry_names_and_registers_jobs()
    {
        var registry = new Registry();
        registry.Schedule(() => NoOp()).WithName("integration-job").ToRunEvery(1).Hours();

        JobManager.Initialize(registry);

        Assert.That(JobManager.HasJob("integration-job"), Is.True);
        Assert.That(JobManager.GetSchedule("integration-job"), Is.Not.Null);
    }

    private static void NoOp()
    {
    }
}
