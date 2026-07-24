using AventusSharp.Data;
using NUnit.Framework;

namespace AventusSharpTest.Data;

[TestFixture]
public class TemporalTypeTests
{
    [Test]
    public void Date_formats_and_compares_at_day_precision()
    {
        var date = new Date(new DateTime(2026, 7, 24, 18, 30, 0));

        Assert.Multiple(() =>
        {
            Assert.That(date.ToString(), Is.EqualTo("2026-07-24"));
            Assert.That(date, Is.EqualTo(new Date(new DateTime(2026, 7, 24, 1, 0, 0))));
            Assert.That(date == new DateTime(2026, 7, 24, 23, 59, 0), Is.True);
            Assert.That(date.Year, Is.EqualTo(2026));
        });
    }

    [Test]
    public void Datetime_formats_and_compares_at_second_precision()
    {
        var value = new Datetime(new DateTime(2026, 7, 24, 18, 30, 12, 999));

        Assert.Multiple(() =>
        {
            Assert.That(value.ToString(), Is.EqualTo("2026-07-24 18-30-12"));
            Assert.That(value, Is.EqualTo(new Datetime(new DateTime(2026, 7, 24, 18, 30, 12, 1))));
            Assert.That(value.DateOnly(), Is.EqualTo(new Date(new DateTime(2026, 7, 24))));
        });
    }

    [Test]
    public void Temporal_comparison_operators_use_underlying_time()
    {
        var earlier = new Date(new DateTime(2026, 1, 1));
        var later = new Date(new DateTime(2026, 1, 2));

        Assert.That(earlier < later, Is.True);
        Assert.That(later >= earlier, Is.True);
    }
}
