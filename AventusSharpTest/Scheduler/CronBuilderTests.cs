using AventusSharp.Scheduler.Cron;
using NUnit.Framework;

namespace AventusSharpTest.Scheduler;

[TestFixture]
public class CronBuilderTests
{
    [Test]
    public void Builder_orders_and_deduplicates_values()
    {
        var expression = new CronBuilder()
            .Second(30, 0, 30)
            .Minute(15, 5)
            .Hour(18)
            .DayOfMonth(24)
            .Month(7)
            .DayOfWeek(5)
            .ToString();

        Assert.That(expression, Is.EqualTo("0,30 5,15 18 24 7 5"));
    }

    [Test]
    public void Builder_supports_step_syntax()
    {
        var expression = new CronBuilder()
            .EachSeconds(10)
            .EachMinutes(5)
            .EachHours(2)
            .ToString();

        Assert.That(expression, Is.EqualTo("*/10 */5 */2 * * *"));
    }

    [TestCase(-1)]
    [TestCase(60)]
    public void Invalid_seconds_are_rejected(int second)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CronBuilder().Second(second));
    }

    [TestCase(0)]
    [TestCase(60)]
    public void Invalid_second_steps_are_rejected(int step)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CronBuilder().EachSeconds(step));
    }
}
