using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataTimestampTests
{
    [SetUp]
    public async Task SetUp()
    {
        var reset = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"timestamped_records\";");
        Assert.That(reset.Success, Is.True, IntegrationEnvironment.ErrorMessages(reset.Errors));
    }

    [Test]
    public async Task Generated_schema_contains_both_timestamp_columns()
    {
        var columns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('timestamped_records');");

        Assert.That(columns.Success, Is.True, IntegrationEnvironment.ErrorMessages(columns.Errors));
        Assert.That(columns.Result!.Select(column => column["name"]),
            Is.SupersetOf(new[] { "Id", "Name", "CreatedDate", "UpdatedDate" }));
    }

    [Test]
    public async Task Create_assigns_created_and_updated_dates_to_the_instance()
    {
        var before = DateTime.Now;
        var item = new TimestampedRecord
        {
            Name = "Created",
            CreatedDate = new DateTime(2000, 1, 1),
            UpdatedDate = new DateTime(2000, 1, 1)
        };

        var result = await TimestampedRecord.CreateWithError(item);
        var after = DateTime.Now;

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(item.CreatedDate, Is.InRange(before, after));
        Assert.That(item.UpdatedDate, Is.InRange(before, after));
        Assert.That((item.UpdatedDate - item.CreatedDate).Duration(),
            Is.LessThan(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task Update_preserves_created_date_and_advances_updated_date()
    {
        var item = await TimestampedRecord.Create(new TimestampedRecord { Name = "Before" });
        Assert.That(item, Is.Not.Null);
        var createdDate = item!.CreatedDate;
        var firstUpdatedDate = item.UpdatedDate;
        await Task.Delay(20);

        item.Name = "After";
        var update = await TimestampedRecord.UpdateWithError(item);
        var stored = await ((TimestampedRecordManager)GenericDM.Get<TimestampedRecord>())
            .GetByIdWithErrorNoCache<TimestampedRecord>(item.Id);

        Assert.That(update.Success, Is.True, IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(item.CreatedDate, Is.EqualTo(createdDate));
        Assert.That(item.UpdatedDate, Is.GreaterThan(firstUpdatedDate));
        Assert.That(stored.Success, Is.True, IntegrationEnvironment.ErrorMessages(stored.Errors));
        Assert.That(stored.Result!.CreatedDate, Is.EqualTo(createdDate));
        Assert.That(stored.Result.UpdatedDate, Is.EqualTo(item.UpdatedDate));
        Assert.That(stored.Result.Name, Is.EqualTo("After"));
    }

    [Test]
    public async Task BulkCreate_assigns_timestamps_to_every_item()
    {
        var items = new List<TimestampedRecord>
        {
            new() { Name = "First" },
            new() { Name = "Second" }
        };
        var before = DateTime.Now;

        var result = await TimestampedRecord.BulkCreateWithError(items);
        var after = DateTime.Now;

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(items, Has.All.Matches<TimestampedRecord>(
            item => item.CreatedDate >= before && item.CreatedDate <= after));
        Assert.That(items, Has.All.Matches<TimestampedRecord>(
            item => item.UpdatedDate >= before && item.UpdatedDate <= after));
    }
}
