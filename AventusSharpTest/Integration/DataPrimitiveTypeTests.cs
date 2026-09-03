using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataPrimitiveTypeTests
{
    private PrimitiveRecordManager Manager =>
        (PrimitiveRecordManager)GenericDM.Get<PrimitiveRecord>();

    [SetUp]
    public async Task ClearTable()
    {
        var result = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"primitive_records\";");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Supported_primitive_types_round_trip_through_the_database()
    {
        var item = new PrimitiveRecord
        {
            SmallNumber = -123,
            LargeNumber = 5_000_000_000,
            SingleNumber = 12.5f,
            DoubleNumber = -98.125,
            DecimalNumber = 12345.625m,
            Letter = 'Z',
            State = PrimitiveRecordState.Ready,
            Duration = new TimeSpan(12, 34, 56),
            StartTime = new TimeOnly(12, 34, 56, 789)
        };

        var creation = await PrimitiveRecord.CreateWithError(item);
        var loaded = await Manager.GetByIdWithErrorNoCache<PrimitiveRecord>(item.Id);

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Result!.SmallNumber, Is.EqualTo(item.SmallNumber));
            Assert.That(loaded.Result.LargeNumber, Is.EqualTo(item.LargeNumber));
            Assert.That(loaded.Result.SingleNumber, Is.EqualTo(item.SingleNumber).Within(0.0001f));
            Assert.That(loaded.Result.DoubleNumber, Is.EqualTo(item.DoubleNumber).Within(0.0000001));
            Assert.That(loaded.Result.DecimalNumber, Is.EqualTo(item.DecimalNumber));
            Assert.That(loaded.Result.Letter, Is.EqualTo(item.Letter));
            Assert.That(loaded.Result.State, Is.EqualTo(item.State));
            Assert.That(loaded.Result.Duration, Is.EqualTo(item.Duration));
            Assert.That(loaded.Result.StartTime, Is.EqualTo(item.StartTime));
        });
    }

    [Test]
    public async Task Primitive_values_can_be_used_by_lambda_queries()
    {
        await PrimitiveRecord.Create(new PrimitiveRecord
        {
            SmallNumber = 2,
            LargeNumber = 20,
            SingleNumber = 2.5f,
            DoubleNumber = 20.5,
            DecimalNumber = 200.25m,
            Letter = 'A',
            State = PrimitiveRecordState.Ready,
            Duration = new TimeSpan(1, 2, 3),
            StartTime = new TimeOnly(8, 15, 30)
        });
        await PrimitiveRecord.Create(new PrimitiveRecord
        {
            SmallNumber = 3,
            LargeNumber = 30,
            SingleNumber = 3.5f,
            DoubleNumber = 30.5,
            DecimalNumber = 300.25m,
            Letter = 'B',
            State = PrimitiveRecordState.Disabled,
            Duration = new TimeSpan(4, 5, 6),
            StartTime = new TimeOnly(16, 45, 15, 125)
        });

        var small = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.SmallNumber == 3);
        var large = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.LargeNumber >= 30);
        var single = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.SingleNumber > 3);
        var @double = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.DoubleNumber < 31 && item.DoubleNumber > 30);
        var @decimal = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.DecimalNumber == 300.25m);
        var expectedLetter = 'B';
        var character = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.Letter == expectedLetter);
        var state = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.State == PrimitiveRecordState.Disabled);
        var expectedDuration = new TimeSpan(4, 5, 6);
        var duration = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.Duration == expectedDuration);
        var expectedStartTime = new TimeOnly(16, 45, 15, 125);
        var startTime = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.StartTime == expectedStartTime);

        Assert.Multiple(() =>
        {
            Assert.That(small.Result, Has.Count.EqualTo(1), "short");
            Assert.That(large.Result, Has.Count.EqualTo(1), "long");
            Assert.That(single.Result, Has.Count.EqualTo(1), "float");
            Assert.That(@double.Result, Has.Count.EqualTo(1), "double");
            Assert.That(@decimal.Result, Has.Count.EqualTo(1), "decimal");
            Assert.That(character.Result, Has.Count.EqualTo(1), "char");
            Assert.That(state.Result, Has.Count.EqualTo(1), "enum");
            Assert.That(duration.Result, Has.Count.EqualTo(1), "TimeSpan");
            Assert.That(startTime.Result, Has.Count.EqualTo(1), "TimeOnly variable");
        });
    }

    [Test]
    public async Task Inline_char_literal_is_translated_as_a_string()
    {
        await PrimitiveRecord.Create(new PrimitiveRecord { Letter = 'B' });

        var result = await Manager.WhereWithErrorNoCache<PrimitiveRecord>(
            item => item.Letter == 'B');

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
    }
}
