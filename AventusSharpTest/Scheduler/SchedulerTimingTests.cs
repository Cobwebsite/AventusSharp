using AventusSharp.Scheduler;
using AventusSharp.Scheduler.Cron;
using AventusSharp.Scheduler.Extension;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
[NonParallelizable]
public sealed class SchedulerTimingTests
{
    [SetUp]
    public void SetUp()
    {
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
    }

    [TearDown]
    public void TearDown()
    {
        JobManager.StopAndBlock();
        JobManager.RemoveAllJobs();
    }

    [TestCase(1, "milliseconds", 1)]
    [TestCase(2, "seconds", 2_000)]
    [TestCase(3, "minutes", 180_000)]
    [TestCase(2, "hours", 7_200_000)]
    public void Fixed_duration_units_calculate_the_expected_next_run(
        int duration,
        string unit,
        double expectedMilliseconds)
    {
        var schedule = new Schedule(() => { });
        var timeUnit = schedule.ToRunEvery(duration);
        switch (unit)
        {
            case "milliseconds":
                timeUnit.Milliseconds();
                break;
            case "seconds":
                timeUnit.Seconds();
                break;
            case "minutes":
                timeUnit.Minutes();
                break;
            case "hours":
                timeUnit.Hours();
                break;
            default:
                throw new AssertionException($"Unknown unit {unit}");
        }
        var before = DateTime.Now;

        JobManager.CalculateNextRun(schedule);
        var elapsed = schedule.NextRun - before;

        Assert.That(elapsed.TotalMilliseconds,
            Is.InRange(expectedMilliseconds - 250, expectedMilliseconds + 1_000));
    }

    [Test]
    public void Delay_is_added_to_the_regular_interval()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(1).Hours().DelayFor(30).Minutes();
        var before = DateTime.Now;

        JobManager.CalculateNextRun(schedule);

        Assert.That(schedule.NextRun - before,
            Is.InRange(TimeSpan.FromMinutes(89.9), TimeSpan.FromMinutes(90.1)));
    }

    [Test]
    public void Run_once_at_an_explicit_time_preserves_that_time()
    {
        var expected = DateTime.Now.AddHours(4);
        var schedule = new Schedule(() => { });
        schedule.ToRunOnceAt(expected);

        JobManager.CalculateNextRun(schedule);

        Assert.That(schedule.NextRun, Is.EqualTo(expected));
    }

    [Test]
    public void Weekdays_only_skips_a_weekend()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(1).Days().At(8, 0).WeekdaysOnly();

        JobManager.CalculateNextRun(schedule);

        Assert.That(schedule.NextRun.DayOfWeek,
            Is.Not.EqualTo(DayOfWeek.Saturday).And.Not.EqualTo(DayOfWeek.Sunday));
        Assert.That(schedule.NextRun.TimeOfDay, Is.EqualTo(TimeSpan.FromHours(8)));
        Assert.That(schedule.NextRun, Is.GreaterThanOrEqualTo(DateTime.Today));
    }

    [Test]
    public void Between_keeps_the_next_execution_inside_the_allowed_window()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(20).Minutes().Between(9, 15, 17, 45);

        JobManager.CalculateNextRun(schedule);

        Assert.That(schedule.NextRun.TimeOfDay,
            Is.GreaterThanOrEqualTo(new TimeSpan(9, 15, 0))
                .And.LessThan(new TimeSpan(17, 45, 0)));
    }

    [Test]
    public void Monthly_day_is_clamped_to_the_last_day_of_a_short_month()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(1).Months().On(31).At(6, 30);

        JobManager.CalculateNextRun(schedule);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.NextRun.Day,
                Is.EqualTo(Math.Min(31,
                    DateTime.DaysInMonth(schedule.NextRun.Year, schedule.NextRun.Month))));
            Assert.That(schedule.NextRun.TimeOfDay, Is.EqualTo(new TimeSpan(6, 30, 0)));
            Assert.That(schedule.NextRun, Is.GreaterThan(DateTime.Now));
        });
    }

    [Test]
    public void Cron_crosses_month_and_year_boundaries()
    {
        var calculator = new CronTimeCalculator("0 30 8 1 1 *");

        var next = calculator.Calculate(new DateTime(2026, 12, 31, 23, 59, 59));

        Assert.That(next, Is.EqualTo(new DateTime(2027, 1, 1, 8, 30, 0)));
    }

    [Test]
    public void Cron_finds_the_next_leap_day()
    {
        var calculator = new CronTimeCalculator("0 0 0 29 2 *");

        var next = calculator.Calculate(new DateTime(2025, 3, 1));

        Assert.That(next, Is.EqualTo(new DateTime(2028, 2, 29)));
    }

    [TestCase("")]
    [TestCase("* * *")]
    [TestCase("60 * * * * *")]
    [TestCase("* * * 32 * *")]
    public void Invalid_cron_expressions_are_rejected(string expression)
    {
        Assert.Throws<CrontabException>(() => new CronTimeCalculator(expression));
    }
}
