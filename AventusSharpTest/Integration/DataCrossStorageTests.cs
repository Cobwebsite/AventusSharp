using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Attributes;
using AventusSharp.Data;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Storage.Sqlite;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataCrossStorageTests
{
    [Test]
    public void Unknown_link_target_returns_a_type_not_found_error()
    {
        var storage = new SqliteStorage(Path.Combine(TestContext.CurrentContext.WorkDirectory, "unresolved-link.db"));
        var manager = new SimpleDatabaseDM<UnresolvedLinkOwner>();
        var registeredManager = GenericDM.Set(typeof(UnresolvedLinkOwner), manager);
        Assert.That(registeredManager.Success, Is.True, IntegrationEnvironment.ErrorMessages(registeredManager.Errors));
        var registeredTable = storage.AddPyramid(new(typeof(UnresolvedLinkOwner), new()));
        Assert.That(registeredTable.Success, Is.True, IntegrationEnvironment.ErrorMessages(registeredTable.Errors));

        var result = storage.CreateLinks();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Has.Some.Matches<AventusSharp.Tools.GenericError>(error =>
            error is DataError dataError && dataError.Code == DataErrorCode.TypeNotFound));
    }

    [Test]
    public async Task Cross_storage_links_keep_their_columns_without_foreign_keys_to_the_other_database()
    {
        var table = IntegrationEnvironment.Storage.GetTableInfo(typeof(CrossStorageOwner))!;
        var single = (ITableMemberInfoSqlLinkSingle)table.Members.Single(member => member.Name == nameof(CrossStorageOwner.Record));
        var multiple = (ITableMemberInfoSqlLinkMultiple)table.Members.Single(member => member.Name == nameof(CrossStorageOwner.Records));

        Assert.Multiple(() =>
        {
            Assert.That(single.TableLinked, Is.Not.Null);
            Assert.That(multiple.TableLinked, Is.Not.Null);
            Assert.That(TableMemberInfoSql.IsLinkInStorage(single, IntegrationEnvironment.Storage), Is.False);
            Assert.That(TableMemberInfoSql.IsLinkInStorage(multiple, IntegrationEnvironment.Storage), Is.False);
        });

        var ownerColumns = await IntegrationEnvironment.Storage.Query("PRAGMA table_info('cross_storage_owners');");
        var ownerKeys = await IntegrationEnvironment.Storage.Query("PRAGMA foreign_key_list('cross_storage_owners');");
        var joinColumns = await IntegrationEnvironment.Storage.Query($"PRAGMA table_info('{multiple.TableIntermediateName}');");
        var joinKeys = await IntegrationEnvironment.Storage.Query($"PRAGMA foreign_key_list('{multiple.TableIntermediateName}');");

        Assert.That(ownerColumns.Success && ownerKeys.Success && joinColumns.Success && joinKeys.Success,
            Is.True, IntegrationEnvironment.ErrorMessages(ownerColumns.Errors.Concat(ownerKeys.Errors).Concat(joinColumns.Errors).Concat(joinKeys.Errors)));
        Assert.That(ownerColumns.Result!.Select(row => row["name"]), Does.Contain("Record"));
        Assert.That(ownerKeys.Result, Has.None.Matches<Dictionary<string, string?>>(row => row["table"] == "dedicated_storage_records"));
        Assert.That(joinColumns.Result!.Select(row => row["name"]), Does.Contain(multiple.TableIntermediateKey2));
        Assert.That(joinKeys.Result, Has.Some.Matches<Dictionary<string, string?>>(row => row["table"] == "cross_storage_owners"));
        Assert.That(joinKeys.Result, Has.None.Matches<Dictionary<string, string?>>(row => row["table"] == "dedicated_storage_records"));
    }

    [SetUp]
    public async Task ClearTables()
    {
        var owners = await IntegrationEnvironment.Storage.Execute("DELETE FROM \"cross_storage_owners\";");
        var records = await DedicatedTestStorage.Instance!.Execute("DELETE FROM \"dedicated_storage_records\";");
        Assert.That(owners.Success && records.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(owners.Errors.Concat(records.Errors)));
    }

    [Test]
    public async Task Ignore_excludes_a_field_in_the_other_storage()
    {
        var record = (await DedicatedStorageRecord.Create(
            new DedicatedStorageRecord { Name = "Remote" }))!;
        var owner = (await CrossStorageOwner.Create(new CrossStorageOwner
        {
            Name = "Local", Record = record
        }))!;

        ((IDatabaseDM)GenericDM.Get<CrossStorageOwner>())
            .RemoveRecordsItems<CrossStorageOwner>([owner.Id]);
        var removedRecord = ((IDatabaseDM)GenericDM.Get<DedicatedStorageRecord>())
            .RemoveRecordsItems<DedicatedStorageRecord>([record.Id]);
        Assert.That(removedRecord, Has.Count.EqualTo(1));

        var result = await CrossStorageOwner.StartQuery()
            .Ignore(item => item.Record!.Name)
            .Where(item => item.Record!.Name == "Remote")
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result![0].Name, Is.EqualTo("Local"));
            Assert.That(result.Result[0].Record!.Id, Is.EqualTo(record.Id));
            Assert.That(result.Result[0].Record!.Name, Is.Empty);
        });
    }

    [Test]
    public async Task Ignore_preserves_a_cached_field_in_the_other_storage()
    {
        var record = (await DedicatedStorageRecord.Create(
            new DedicatedStorageRecord { Name = "Remote" }))!;
        var owner = (await CrossStorageOwner.Create(new CrossStorageOwner
        {
            Name = "Local", Record = record
        }))!;

        var result = await CrossStorageOwner.StartQuery()
            .Ignore(item => item.Record!.Name)
            .Where(item => item.Record!.Name == "Remote")
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result![0].Record, Is.SameAs(record));
            Assert.That(result.Result[0].Record!.Name, Is.EqualTo("Remote"));
        });
    }

    [Test]
    public async Task Ignore_excludes_a_collection_in_the_other_storage()
    {
        var record = (await DedicatedStorageRecord.Create(
            new DedicatedStorageRecord { Name = "Remote" }))!;
        var owner = (await CrossStorageOwner.Create(new CrossStorageOwner
        {
            Name = "Local", Records = [record]
        }))!;

        ((IDatabaseDM)GenericDM.Get<CrossStorageOwner>())
            .RemoveRecordsItems<CrossStorageOwner>([owner.Id]);
        ((IDatabaseDM)GenericDM.Get<DedicatedStorageRecord>())
            .RemoveRecordsItems<DedicatedStorageRecord>([record.Id]);

        var result = await CrossStorageOwner.StartQuery()
            .Ignore(item => item.Records)
            .Where(item => item.Records.Any(value => value.Name == "Remote"))
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(result.Result![0].Records, Is.Empty);
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

[ManualInit]
public sealed class UnresolvedLinkOwner : AventusSharp.Data.Storable<UnresolvedLinkOwner>
{
    public UnregisteredLinkTarget? Target { get; set; }
}

[ManualInit]
public sealed class UnregisteredLinkTarget : AventusSharp.Data.Storable<UnregisteredLinkTarget>
{
}
