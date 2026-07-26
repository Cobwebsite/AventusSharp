using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataNullableTypeTests
{
    private NullablePrimitiveRecordManager Manager =>
        (NullablePrimitiveRecordManager)GenericDM.Get<NullablePrimitiveRecord>();

    [SetUp]
    public async Task ClearTable()
    {
        var result = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"nullable_primitive_records\";");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Nullable_value_types_round_trip_with_values_and_nulls()
    {
        var withValues = new NullablePrimitiveRecord
        {
            Number = 42,
            Amount = 123.75m,
            Enabled = true,
            State = PrimitiveRecordState.Ready,
            Duration = new TimeSpan(5, 4, 3)
        };
        var withNulls = new NullablePrimitiveRecord();

        var firstCreation = await NullablePrimitiveRecord.CreateWithError(withValues);
        var secondCreation = await NullablePrimitiveRecord.CreateWithError(withNulls);
        var first = await Manager.GetByIdWithErrorNoCache<NullablePrimitiveRecord>(withValues.Id);
        var second = await Manager.GetByIdWithErrorNoCache<NullablePrimitiveRecord>(withNulls.Id);

        Assert.That(firstCreation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(firstCreation.Errors));
        Assert.That(secondCreation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondCreation.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(first.Result!.Number, Is.EqualTo(42));
            Assert.That(first.Result.Amount, Is.EqualTo(123.75m));
            Assert.That(first.Result.Enabled, Is.True);
            Assert.That(first.Result.State, Is.EqualTo(PrimitiveRecordState.Ready));
            Assert.That(first.Result.Duration, Is.EqualTo(new TimeSpan(5, 4, 3)));
            Assert.That(second.Result!.Number, Is.Null);
            Assert.That(second.Result.Amount, Is.Null);
            Assert.That(second.Result.Enabled, Is.Null);
            Assert.That(second.Result.State, Is.Null);
            Assert.That(second.Result.Duration, Is.Null);
        });
    }

    [Test]
    public async Task Nullable_value_types_support_null_and_value_queries()
    {
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord());
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord
        {
            Number = 42,
            Enabled = false,
            State = PrimitiveRecordState.Disabled
        });

        var nulls = await Manager.WhereWithErrorNoCache<NullablePrimitiveRecord>(
            item => item.Number == null && item.Enabled == null && item.State == null);
        int? expectedNumber = 42;
        var values = await Manager.WhereWithErrorNoCache<NullablePrimitiveRecord>(
            item => item.Number == expectedNumber
                && item.Enabled == false
                && item.State == PrimitiveRecordState.Disabled);

        Assert.That(nulls.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(nulls.Errors));
        Assert.That(values.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(values.Errors));
        Assert.That(nulls.Result, Has.Count.EqualTo(1));
        Assert.That(values.Result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task HasValue_and_Value_can_be_used_in_queries()
    {
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord());
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord { Number = 42 });
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord { Number = 10 });

        var result = await Manager.WhereWithErrorNoCache<NullablePrimitiveRecord>(
            item => item.Number.HasValue && item.Number.Value >= 40);

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Number),
            Is.EqualTo(new int?[] { 42 }));
    }

    [Test]
    [Explicit("Specification: GetValueOrDefault requires SQL COALESCE translation.")]
    public async Task GetValueOrDefault_can_be_used_in_queries()
    {
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord());
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord { Number = 42 });

        var defaults = await Manager.WhereWithErrorNoCache<NullablePrimitiveRecord>(
            item => item.Number.GetValueOrDefault() == 0);
        var values = await Manager.WhereWithErrorNoCache<NullablePrimitiveRecord>(
            item => item.Number.GetValueOrDefault() == 42);

        Assert.That(defaults.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(defaults.Errors));
        Assert.That(values.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(values.Errors));
        Assert.That(defaults.Result, Has.Count.EqualTo(1));
        Assert.That(values.Result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Nullable_collection_contains_matches_non_null_values()
    {
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord());
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord { Number = 42 });
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord { Number = 10 });
        var accepted = new List<int?> { 42, 42 };

        var result = await Manager.WhereWithErrorNoCache<NullablePrimitiveRecord>(
            item => accepted.Contains(item.Number));

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Number),
            Is.EqualTo(new int?[] { 42 }));
    }

    [Test]
    [Explicit("Specification: SQL IN(NULL, ...) does not implement Contains(null) semantics.")]
    public async Task Nullable_collection_contains_matches_null()
    {
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord());
        await NullablePrimitiveRecord.Create(new NullablePrimitiveRecord { Number = 42 });
        var accepted = new List<int?> { null, 42 };

        var result = await Manager.WhereWithErrorNoCache<NullablePrimitiveRecord>(
            item => accepted.Contains(item.Number));

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(2));
    }
}
