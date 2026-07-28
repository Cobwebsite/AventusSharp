using AventusSharp.Scheduler;
using AventusSharp.Scheduler.Extension;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
[NonParallelizable]
public sealed class SchedulerCalendarTests
{
    [Test]
    public void Daily_at_uses_today_when_time_is_future_otherwise_the_next_day()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(1).Days().At(14, 30);

        Assert.Multiple(() =>
        {
            Assert.That(Next(schedule, new DateTime(2026, 7, 27, 10, 0, 0)),
                Is.EqualTo(new DateTime(2026, 7, 27, 14, 30, 0)));
            Assert.That(Next(schedule, new DateTime(2026, 7, 27, 15, 0, 0)),
                Is.EqualTo(new DateTime(2026, 7, 28, 14, 30, 0)));
        });
    }

    [Test]
    public void Hourly_at_uses_the_requested_minute()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(2).Hours().At(15);

        Assert.Multiple(() =>
        {
            Assert.That(Next(schedule, new DateTime(2026, 7, 27, 10, 5, 0)),
                Is.EqualTo(new DateTime(2026, 7, 27, 12, 15, 0)));
            Assert.That(Next(schedule, new DateTime(2026, 7, 27, 10, 20, 0)),
                Is.EqualTo(new DateTime(2026, 7, 27, 12, 15, 0)));
        });
    }

    [Test]
    public void Weekdays_skip_the_entire_weekend()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(1).Weekdays().At(9, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(Next(schedule, new DateTime(2026, 7, 24, 10, 0, 0)),
                Is.EqualTo(new DateTime(2026, 7, 27, 9, 0, 0)));
            Assert.That(Next(schedule, new DateTime(2026, 7, 25, 10, 0, 0)),
                Is.EqualTo(new DateTime(2026, 7, 27, 9, 0, 0)));
        });
    }

    [Test]
    public void Weekly_on_a_day_uses_the_requested_weekday_and_time()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(1).Weeks().On(DayOfWeek.Wednesday).At(8, 45);

        Assert.Multiple(() =>
        {
            Assert.That(Next(schedule, new DateTime(2026, 7, 27, 10, 0, 0)),
                Is.EqualTo(new DateTime(2026, 8, 5, 8, 45, 0)));
            Assert.That(Next(schedule, new DateTime(2026, 7, 29, 9, 0, 0)),
                Is.EqualTo(new DateTime(2026, 8, 5, 8, 45, 0)));
        });
    }

    [Test]
    public void Monthly_on_31_clamps_to_february_end()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(1).Months().On(31).At(6, 0);

        var next = Next(schedule, new DateTime(2027, 2, 1));

        Assert.That(next, Is.EqualTo(new DateTime(2027, 2, 28, 6, 0, 0)));
    }

    [Test]
    public void Monthly_last_day_handles_a_leap_year()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(1).Months().OnTheLastDay().At(23, 30);

        var next = Next(schedule, new DateTime(2028, 2, 10));

        Assert.That(next, Is.EqualTo(new DateTime(2028, 2, 29, 23, 30, 0)));
    }

    [Test]
    public void Monthly_first_and_last_weekday_are_calculated_correctly()
    {
        var first = new Schedule(() => { });
        first.ToRunEvery(1).Months().OnTheFirst(DayOfWeek.Monday).At(9, 0);
        var last = new Schedule(() => { });
        last.ToRunEvery(1).Months().OnTheLast(DayOfWeek.Friday).At(17, 0);

        Assert.Multiple(() =>
        {
            Assert.That(Next(first, new DateTime(2026, 8, 1)),
                Is.EqualTo(new DateTime(2026, 8, 3, 9, 0, 0)));
            Assert.That(Next(last, new DateTime(2026, 8, 1)),
                Is.EqualTo(new DateTime(2026, 8, 28, 17, 0, 0)));
        });
    }

    [Test]
    public void Between_moves_an_early_result_to_the_opening_time()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(20).Minutes().Between(9, 15, 17, 45);

        var next = Next(schedule, new DateTime(2026, 7, 27, 8, 0, 0));

        Assert.That(next, Is.EqualTo(new DateTime(2026, 7, 27, 9, 15, 0)));
    }

    [Test]
    public void Between_skips_late_results_while_preserving_the_interval()
    {
        var schedule = new Schedule(() => { });
        schedule.ToRunEvery(30).Minutes().Between(9, 0, 17, 0);

        var next = Next(schedule, new DateTime(2026, 7, 27, 16, 50, 0));

        Assert.That(next, Is.EqualTo(new DateTime(2026, 7, 28, 9, 20, 0)));
    }

    private static DateTime Next(Schedule schedule, DateTime from)
    {
        Assert.That(schedule.CalculateNextRun, Is.Not.Null);
        return schedule.CalculateNextRun!(from);
    }
}
