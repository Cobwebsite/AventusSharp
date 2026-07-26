using AventusSharp.Data;
using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataAttributeTests
{
    [SetUp]
    public async Task SetUp()
    {
        var reset = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"attribute_records\";" +
            "DELETE FROM \"failing_bulk_transform_records\";");
        Assert.That(reset.Success, Is.True, IntegrationEnvironment.ErrorMessages(reset.Errors));
    }

    [Test]
    public async Task Default_attribute_generates_a_database_default()
    {
        var columns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('attribute_records');");
        Assert.That(columns.Success, Is.True, IntegrationEnvironment.ErrorMessages(columns.Errors));
        var priority = columns.Result!.Single(column => column["name"] == "Priority");

        var insert = await IntegrationEnvironment.Storage.Execute(
            "INSERT INTO \"attribute_records\" (\"Code\", \"EvenValue\", \"RequiredText\") " +
            "VALUES ('DEFAULT', 2, 'present');");
        var row = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Priority\", \"Category\" FROM \"attribute_records\" WHERE \"Code\" = 'DEFAULT';");
        var category = columns.Result!.Single(column => column["name"] == "Category");

        Assert.That(priority["dflt_value"], Is.EqualTo("7"));
        Assert.That(category["dflt_value"], Is.EqualTo("'standard'"));
        Assert.That(insert.Success, Is.True, IntegrationEnvironment.ErrorMessages(insert.Errors));
        Assert.That(row.Result!.Single()["Priority"], Is.EqualTo("7"));
        Assert.That(row.Result!.Single()["Category"], Is.EqualTo("standard"));
    }

    [Test]
    public async Task SqlTransform_converts_values_to_and_from_the_database()
    {
        var item = new AttributeRecord
        {
            Code = "mixedCase",
            EvenValue = 2,
            RequiredText = "present"
        };

        var creation = await AttributeRecord.CreateWithError(item);
        var raw = await IntegrationEnvironment.Storage.Query(
            $"SELECT \"Code\" FROM \"attribute_records\" WHERE \"Id\" = {item.Id};");
        var loaded = await ((AttributeRecordManager)GenericDM.Get<AttributeRecord>())
            .GetByIdWithErrorNoCache<AttributeRecord>(item.Id);

        Assert.That(creation.Success, Is.True, IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(raw.Result!.Single()["Code"], Is.EqualTo("MIXEDCASE"));
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Code, Is.EqualTo("mixedcase"));
    }

    [Test]
    public async Task BulkCreate_withId_normalizes_the_canonical_instance_through_SqlTransform()
    {
        var item = new AttributeRecord
        {
            Id = 110_001,
            Code = "MiXeD-BuLk",
            EvenValue = 2,
            RequiredText = "present"
        };

        var creation = await AttributeRecord.BulkCreateWithError([item], withId: true);
        var raw = await IntegrationEnvironment.Storage.Query(
            $"SELECT \"Code\" FROM \"attribute_records\" WHERE \"Id\" = {item.Id};");
        var cached = await AttributeRecord.GetByIdWithError(item.Id);
        var noCache = await ((AttributeRecordManager)GenericDM.Get<AttributeRecord>())
            .GetByIdWithErrorNoCache<AttributeRecord>(item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(creation.Errors));
            Assert.That(raw.Result!.Single()["Code"], Is.EqualTo("MIXED-BULK"));
            Assert.That(cached.Result, Is.SameAs(item));
            Assert.That(item.Code, Is.EqualTo("mixed-bulk"));
            Assert.That(noCache.Result!.Code, Is.EqualTo(item.Code));
        });
    }

    [Test]
    public async Task BulkCreate_withId_rollback_restores_values_changed_by_SqlTransform_normalization()
    {
        var item = new AttributeRecord
        {
            Id = 110_002,
            Code = "RoLlBaCk-BuLk",
            EvenValue = 2,
            RequiredText = "present"
        };
        var manager = GenericDM.Get<AttributeRecord>();

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var creation = await AttributeRecord.BulkCreateWithError([item], withId: true);
            creation.Errors.Add(new AventusSharp.Tools.GenericError(
                9917, "force transformed bulk rollback"));
            return creation;
        });
        var rows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"attribute_records\" WHERE \"Id\" = {item.Id};");
        var cached = await AttributeRecord.GetByIdWithError(item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(transaction.Success, Is.False);
            Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
            Assert.That(cached.Success, Is.False);
            Assert.That(cached.Result, Is.Null);
            Assert.That(item.Code, Is.EqualTo("RoLlBaCk-BuLk"),
                "Rollback must restore values mutated only to canonicalize the cache.");
        });
    }

    [Test]
    public async Task BulkCreate_normalization_failure_rolls_back_prior_transforms_and_storage()
    {
        var item = new FailingBulkTransformRecord
        {
            Id = 110_003,
            NormalizedBeforeFailure = "MiXeD-BeFoRe-FaIlUrE",
            FailingValue = "trigger"
        };

        var creation = await FailingBulkTransformRecord.BulkCreateWithError(
            [item], withId: true);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"failing_bulk_transform_records\";");
        var cached = await FailingBulkTransformRecord.GetByIdWithError(item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.False);
            Assert.That(IntegrationEnvironment.ErrorMessages(creation.Errors),
                Does.Contain("intentional FromSql failure"));
            Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
            Assert.That(cached.Success, Is.False);
            Assert.That(cached.Result, Is.Null);
            Assert.That(item.NormalizedBeforeFailure,
                Is.EqualTo("MiXeD-BeFoRe-FaIlUrE"));
            Assert.That(item.FailingValue, Is.EqualTo("trigger"));
        });
    }

    [Test]
    public async Task Second_item_normalization_failure_restores_all_prior_bulk_items()
    {
        var first = new FailingBulkTransformRecord
        {
            Id = 110_004,
            NormalizedBeforeFailure = "FiRsT-NoRmAlIzEd",
            FailingValue = "safe"
        };
        var second = new FailingBulkTransformRecord
        {
            Id = 110_005,
            NormalizedBeforeFailure = "SeCoNd-BeFoRe-FaIlUrE",
            FailingValue = "trigger"
        };

        var creation = await FailingBulkTransformRecord.BulkCreateWithError(
            [first, second], withId: true);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"failing_bulk_transform_records\";");
        var firstCached = await FailingBulkTransformRecord.GetByIdWithError(first.Id);
        var secondCached = await FailingBulkTransformRecord.GetByIdWithError(second.Id);

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.False);
            Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
            Assert.That(firstCached.Success, Is.False);
            Assert.That(secondCached.Success, Is.False);
            Assert.That(first.NormalizedBeforeFailure, Is.EqualTo("FiRsT-NoRmAlIzEd"));
            Assert.That(first.FailingValue, Is.EqualTo("safe"));
            Assert.That(second.NormalizedBeforeFailure,
                Is.EqualTo("SeCoNd-BeFoRe-FaIlUrE"));
            Assert.That(second.FailingValue, Is.EqualTo("trigger"));
        });
    }

    [Test]
    public async Task Custom_validation_attribute_returns_a_structured_field_error()
    {
        var result = await AttributeRecord.CreateWithError(new AttributeRecord
        {
            Code = "odd",
            EvenValue = 3,
            RequiredText = "present"
        });

        Assert.That(result.Success, Is.False);
        var error = result.Errors.OfType<DataError>()
            .Single(item => item.Code == DataErrorCode.ValidationError);
        Assert.That(error.Message, Does.Contain("Create"));
        Assert.That(error.Details.OfType<FieldErrorInfo>().Select(detail => detail.Name),
            Does.Contain(nameof(AttributeRecord.EvenValue)));
    }

    [Test]
    public async Task NotNullable_uses_its_custom_message_before_sql_execution()
    {
        var result = await AttributeRecord.CreateWithError(new AttributeRecord
        {
            Code = "missing",
            EvenValue = 2,
            RequiredText = null
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.OfType<DataError>().Select(error => error.Message),
            Does.Contain("RequiredText must be provided"));
        var count = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"attribute_records\";");
        Assert.That(count.Result!.Single()["count"], Is.EqualTo("0"));
    }
}
