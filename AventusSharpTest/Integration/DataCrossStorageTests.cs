using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataCrossStorageTests
{
    [SetUp]
    public async Task ClearTables()
    {
        var owners = await IntegrationEnvironment.Storage.Execute("DELETE FROM \"cross_storage_owners\";");
        var records = await DedicatedTestStorage.Instance!.Execute("DELETE FROM \"dedicated_storage_records\";");
        Assert.That(owners.Success && records.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(owners.Errors.Concat(records.Errors)));
    }

    [Test]
    public async Task Link_can_be_loaded_and_filtered_across_two_storages()
    {
        var record = (await DedicatedStorageRecord.Create(
            new DedicatedStorageRecord { Name = "Remote" }))!;
        var owner = (await CrossStorageOwner.Create(new CrossStorageOwner
        {
            Name = "Local", Record = record
        }))!;

        ((IDatabaseDM)GenericDM.Get<CrossStorageOwner>())
            .RemoveRecordsItems<CrossStorageOwner>([owner.Id]);
        ((IDatabaseDM)GenericDM.Get<DedicatedStorageRecord>())
            .RemoveRecordsItems<DedicatedStorageRecord>([record.Id]);

        var all = await CrossStorageOwner.StartQuery()
            .Include(item => item.Record)
            .RunWithError();
        Assert.That(all.Success, Is.True, IntegrationEnvironment.ErrorMessages(all.Errors));
        Assert.That(all.Result, Has.Count.EqualTo(1));
        Assert.That(all.Result![0].Record!.Name, Is.EqualTo("Remote"));

        var query = CrossStorageOwner.StartQuery()
            .Include(item => item.Record)
            .Where(item => item.Record!.Name == "Remote");
        var loaded = await query.RunWithError();

        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result, Has.Count.EqualTo(1));
        Assert.That(loaded.Result![0].Record!.Id, Is.EqualTo(record.Id));
        Assert.That(loaded.Result[0].Record!.Name, Is.EqualTo("Remote"));
    }

    [Test]
    public async Task Collection_can_be_loaded_across_two_storages()
    {
        var first = (await DedicatedStorageRecord.Create(
            new DedicatedStorageRecord { Name = "First" }))!;
        var second = (await DedicatedStorageRecord.Create(
            new DedicatedStorageRecord { Name = "Second" }))!;
        var owner = (await CrossStorageOwner.Create(new CrossStorageOwner
        {
            Name = "Collection", Records = [first, second]
        }))!;

        ((IDatabaseDM)GenericDM.Get<CrossStorageOwner>())
            .RemoveRecordsItems<CrossStorageOwner>([owner.Id]);
        ((IDatabaseDM)GenericDM.Get<DedicatedStorageRecord>())
            .RemoveRecordsItems<DedicatedStorageRecord>([first.Id, second.Id]);

        var result = await CrossStorageOwner.StartQuery()
            .Include(item => item.Records)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(result.Result![0].Records.Select(item => item.Name),
            Is.EquivalentTo(new[] { "First", "Second" }));

        var filtered = await CrossStorageOwner.StartQuery()
            .Where(item => item.Records.Any(record => record.Name == "Second"))
            .RunWithError();
        Assert.That(filtered.Success, Is.True, IntegrationEnvironment.ErrorMessages(filtered.Errors));
        Assert.That(filtered.Result!.Select(item => item.Id), Is.EqualTo(new[] { owner.Id }));
    }

    [Test]
    public async Task Reverse_link_can_be_loaded_across_two_storages()
    {
        var record = (await DedicatedStorageRecord.Create(
            new DedicatedStorageRecord { Name = "Parent" }))!;
        var owner = (await CrossStorageOwner.Create(new CrossStorageOwner
        {
            Name = "Child", Record = record
        }))!;

        ((IDatabaseDM)GenericDM.Get<CrossStorageOwner>())
            .RemoveRecordsItems<CrossStorageOwner>([owner.Id]);
        ((IDatabaseDM)GenericDM.Get<DedicatedStorageRecord>())
            .RemoveRecordsItems<DedicatedStorageRecord>([record.Id]);

        var result = await DedicatedStorageRecord.StartQuery()
            .Include(item => item.Owners)
            .Where(item => item.Owners.Any(owner => owner.Name == "Child"))
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(result.Result![0].Owners.Select(item => item.Id), Is.EqualTo(new[] { owner.Id }));
    }
}
