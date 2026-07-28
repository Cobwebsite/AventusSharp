using AventusSharp.Scheduler;
using AventusSharp.Scheduler.Extension;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
[NonParallelizable]
public sealed class SchedulerAdditionalCoverageTests
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

    [Test]
    public void Monthly_second_third_and_fourth_weekdays_are_calculated()
    {
        var second = new Schedule(() => { });
        second.ToRunEvery(1).Months().OnTheSecond(DayOfWeek.Tuesday).At(8, 0);
        var third = new Schedule(() => { });
        third.ToRunEvery(1).Months().OnTheThird(DayOfWeek.Wednesday).At(9, 0);
        var fourth = new Schedule(() => { });
        fourth.ToRunEvery(1).Months().OnTheFourth(DayOfWeek.Thursday).At(10, 0);
        var from = new DateTime(2026, 8, 1);

        Assert.Multiple(() =>
        {
            Assert.That(Next(second, from),
                Is.EqualTo(new DateTime(2026, 8, 11, 8, 0, 0)));
            Assert.That(Next(third, from),
                Is.EqualTo(new DateTime(2026, 8, 19, 9, 0, 0)));
            Assert.That(Next(fourth, from),
                Is.EqualTo(new DateTime(2026, 8, 27, 10, 0, 0)));
        });
    }

    [Test]
    public void Multi_year_interval_advances_by_the_requested_number_of_years()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(3).Years();

        var next = Next(schedule, new DateTime(2026, 7, 27, 12, 0, 0));

        Assert.That(next, Is.EqualTo(new DateTime(2029, 7, 27)));
    }

    [Test]
    public void Multi_week_interval_advances_by_the_requested_number_of_weeks()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(3).Weeks();

        var next = Next(schedule, new DateTime(2026, 7, 27, 12, 0, 0));

        Assert.That(next, Is.EqualTo(new DateTime(2026, 8, 17)));
    }

    [TestCase(2, "seconds", 2_000)]
    [TestCase(3, "minutes", 180_000)]
    [TestCase(4, "hours", 14_400_000)]
    [TestCase(2, "days", 172_800_000)]
    [TestCase(2, "weeks", 1_209_600_000)]
    public void Delay_for_supports_fixed_duration_units(
        int duration,
        string unit,
        double expectedMilliseconds)
    {
        var schedule = new Schedule(() => { });
        var delay = schedule.ToRunNow().DelayFor(duration);
        switch (unit)
        {
            case "seconds":
                delay.Seconds();
                break;
            case "minutes":
                delay.Minutes();
                break;
            case "hours":
                delay.Hours();
                break;
            case "days":
                delay.Days();
                break;
            case "weeks":
                delay.Weeks();
                break;
        }
        var before = DateTime.Now;

        JobManager.CalculateNextRun(schedule);

        Assert.That((schedule.NextRun - before).TotalMilliseconds,
            Is.InRange(expectedMilliseconds - 250, expectedMilliseconds + 1_000));
    }

    [Test]
    public void Delay_for_months_and_years_uses_calendar_durations()
    {
        var monthSchedule = new Schedule(() => { });
        monthSchedule.ToRunNow().DelayFor(2).Months();
        var yearSchedule = new Schedule(() => { });
        yearSchedule.ToRunNow().DelayFor(1).Years();
        var today = DateTime.Today;

        JobManager.CalculateNextRun(monthSchedule);
        JobManager.CalculateNextRun(yearSchedule);

        Assert.Multiple(() =>
        {
            Assert.That(monthSchedule.NextRun - DateTime.Now,
                Is.EqualTo(today.AddMonths(2) - today).Within(TimeSpan.FromSeconds(1)));
            Assert.That(yearSchedule.NextRun - DateTime.Now,
                Is.EqualTo(today.AddYears(1) - today).Within(TimeSpan.FromSeconds(1)));
        });
    }

    [Test]
    public void Run_once_at_clock_time_uses_tomorrow_when_time_has_passed()
    {
        var now = DateTime.Now;
        var passed = now.AddMinutes(-2);
        var schedule = new Schedule(() => { });

        schedule.ToRunOnceAt(passed.Hour, passed.Minute);
        JobManager.CalculateNextRun(schedule);

        var expected = DateTime.Today.AddDays(1)
            .AddHours(passed.Hour)
            .AddMinutes(passed.Minute);
        Assert.That(schedule.NextRun, Is.EqualTo(expected));
    }

    [Test]
    public void Schedule_cron_builder_and_string_produce_the_same_next_run()
    {
        var from = new DateTime(2026, 7, 27, 10, 12, 0);
        var built = new Schedule(() => { });
        built.Cron(builder => builder.Second(0).Minute(30).EachHours());
        var parsed = new Schedule(() => { });
        parsed.Cron("0 30 * * * *");

        Assert.That(Next(built, from), Is.EqualTo(Next(parsed, from)));
        Assert.That(Next(built, from),
            Is.EqualTo(new DateTime(2026, 7, 27, 10, 30, 0)));
    }

    [Test]
    public void Null_jobs_and_scheduling_callbacks_are_rejected()
    {
        var registry = new Registry();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() =>
                registry.Schedule((System.Linq.Expressions.Expression<Action>)null!));
            Assert.Throws<ArgumentNullException>(() =>
                registry.Schedule((IJob)null!));
            Assert.Throws<ArgumentNullException>(() =>
                registry.Schedule((Func<IJob>)null!));
            Assert.Throws<ArgumentNullException>(() =>
                new Schedule(() => { }).AndThen((Action)null!));
            Assert.Throws<ArgumentNullException>(() =>
                JobManager.AddJob((Action)null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() =>
                JobManager.AddJob(() => { }, null!));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Recurring_and_single_delayed_runs_reject_non_positive_intervals(
        int interval)
    {
        var chained = new Schedule(() => { });
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Schedule(() => { }).ToRunEvery(interval));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Schedule(() => { }).ToRunOnceIn(interval));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                chained.ToRunNow().AndEvery(interval));
            Assert.That(chained.AdditionalSchedules, Is.Empty);
        });
    }

    [Test]
    public void Delay_for_rejects_negative_but_accepts_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Schedule(() => { }).ToRunEvery(1).Seconds().DelayFor(-1));
        Assert.DoesNotThrow(() =>
            new Schedule(() => { }).ToRunEvery(1).Seconds().DelayFor(0).Seconds());
    }

    private static DateTime Next(Schedule schedule, DateTime from)
    {
        Assert.That(schedule.CalculateNextRun, Is.Not.Null);
        return schedule.CalculateNextRun!(from);
    }
}
