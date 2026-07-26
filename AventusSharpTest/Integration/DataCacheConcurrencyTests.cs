using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataCacheConcurrencyTests
{
    [SetUp]
    public async Task ClearTable()
    {
        var result = await Device.StartDelete()
            .Where(device => device.Id > 0)
            .RunWithError();
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Concurrent_cache_misses_resolve_to_one_shared_instance()
    {
        var insert = await IntegrationEnvironment.Storage.Execute(
            "INSERT INTO \"devices\" " +
            "(\"Name\", \"Room\", \"Brightness\", \"PowerConsumption\", \"IsOnline\", \"InstalledOn\", \"LastSeen\") " +
            "VALUES ('Concurrent', 'Lab', 10, 1.0, 1, '2026-01-01', '2026-01-01 10:00:00');");
        var idResult = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Id\" AS id FROM \"devices\" WHERE \"Name\" = 'Concurrent';");
        Assert.That(insert.Success, Is.True, IntegrationEnvironment.ErrorMessages(insert.Errors));
        Assert.That(idResult.Success, Is.True, IntegrationEnvironment.ErrorMessages(idResult.Errors));
        var id = int.Parse(idResult.Result!.Single()["id"]!);

        var manager = GenericDM.Get<Device>();
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => manager.GetByIdWithError<Device>(id))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.That(results.All(result => result.Success), Is.True,
            string.Join(Environment.NewLine,
                results.SelectMany(result => result.Errors).Select(error => error.Message)));
        var instances = results.Select(result => result.Result).ToArray();
        Assert.That(instances, Has.All.Not.Null);
        Assert.That(instances.Skip(1).All(instance => ReferenceEquals(instances[0], instance)), Is.True,
            "Every concurrent read of one cached id must return the same object instance.");
    }

    [Test]
    public async Task First_where_loads_the_complete_cache_and_all_reads_share_instances()
    {
        var insert = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"cache_probe_records\";" +
            "INSERT INTO \"cache_probe_records\" (\"Name\", \"Value\") VALUES ('Match', 20);" +
            "INSERT INTO \"cache_probe_records\" (\"Name\", \"Value\") VALUES ('Other', 5);");
        Assert.That(insert.Success, Is.True, IntegrationEnvironment.ErrorMessages(insert.Errors));

        var filtered = await CacheProbeRecord.WhereWithError(item => item.Value >= 10);
        var all = await CacheProbeRecord.GetAllWithError();
        var byId = await CacheProbeRecord.GetByIdWithError(filtered.Result!.Single().Id);
        var single = await CacheProbeRecord.SingleWithError(item => item.Name == "Match");
        var existsWithLocalOnlyExpression = await CacheProbeRecord.ExistWithError(
            item => item.Name.Trim().ToLowerInvariant() == "match");

        Assert.Multiple(() =>
        {
            Assert.That(filtered.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(filtered.Errors));
            Assert.That(filtered.Result, Has.Count.EqualTo(1));
            Assert.That(all.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(all.Errors));
            Assert.That(all.Result, Has.Count.EqualTo(2),
                "The first cached Where must populate Records with every row of the type.");
            Assert.That(all.Result!.Single(item => item.Name == "Match"),
                Is.SameAs(filtered.Result!.Single()));
            Assert.That(byId.Result, Is.SameAs(filtered.Result!.Single()));
            Assert.That(single.Result, Is.SameAs(filtered.Result!.Single()));
            Assert.That(existsWithLocalOnlyExpression.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(existsWithLocalOnlyExpression.Errors));
            Assert.That(existsWithLocalOnlyExpression.Result, Is.True);
        });
    }

    [Test]
    public async Task BulkCreate_updates_an_already_complete_local_cache()
    {
        var manager = GenericDM.Get<Device>();
        var initial = await manager.GetAllWithError<Device>();
        Assert.That(initial.Success, Is.True, IntegrationEnvironment.ErrorMessages(initial.Errors));
        Assert.That(initial.Result, Is.Empty);

        var devices = new List<Device>
        {
            NewDevice("Bulk cached one", 10),
            NewDevice("Bulk cached two", 20)
        };
        var creation = await Device.BulkCreateWithError(devices);
        var loaded = await manager.GetAllWithError<Device>();

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result, Has.Count.EqualTo(2));
        Assert.That(loaded.Result!.Select(item => item.Name),
            Is.EquivalentTo(new[] { "Bulk cached one", "Bulk cached two" }));
        Assert.That(loaded.Result, Has.All.Property(nameof(Device.Id)).GreaterThan(0));
    }

    [Test]
    public async Task BulkCreate_withId_uses_the_supplied_instances_as_canonical_cache_entries()
    {
        var manager = GenericDM.Get<Device>();
        var initial = await manager.GetAllWithError<Device>();
        Assert.That(initial.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(initial.Errors));
        Assert.That(initial.Result, Is.Empty);

        var first = NewDevice("Explicit cached bulk one", 10);
        first.Id = 80_001;
        first.RuntimeState = "first runtime state";
        var second = NewDevice("Explicit cached bulk two", 20);
        second.Id = 80_002;
        second.RuntimeState = "second runtime state";

        var creation = await Device.BulkCreateWithError([first, second], withId: true);
        var firstById = await Device.GetByIdWithError(first.Id);
        var all = await manager.GetAllWithError<Device>();

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(creation.Errors));
            Assert.That(firstById.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(firstById.Errors));
            Assert.That(firstById.Result, Is.SameAs(first));
            Assert.That(firstById.Result!.RuntimeState, Is.EqualTo("first runtime state"));
            Assert.That(all.Result!.Single(item => item.Id == first.Id), Is.SameAs(first));
            Assert.That(all.Result!.Single(item => item.Id == second.Id), Is.SameAs(second));
        });
    }

    [Test]
    public async Task BulkCreate_withId_outer_rollback_removes_supplied_instances_from_cache()
    {
        var manager = GenericDM.Get<Device>();
        var item = NewDevice("Explicit bulk rollback", 10);
        item.Id = 80_003;

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var creation = await Device.BulkCreateWithError([item], withId: true);
            creation.Errors.Add(new GenericError(9916, "force explicit bulk rollback"));
            return creation;
        });
        var stored = await ((DeviceManager)manager)
            .GetByIdWithErrorNoCache<Device>(item.Id);
        var cached = await Device.GetByIdWithError(item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(transaction.Success, Is.False);
            Assert.That(stored.Success, Is.False);
            Assert.That(stored.Result, Is.Null);
            Assert.That(cached.Success, Is.False);
            Assert.That(cached.Result, Is.Null);
        });
    }

    [Test]
    public async Task Failed_BulkCreate_withId_second_buffer_keeps_complete_cache_empty()
    {
        var manager = GenericDM.Get<Device>();
        var initial = await manager.GetAllWithError<Device>();
        Assert.That(initial.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(initial.Errors));
        Assert.That(initial.Result, Is.Empty);

        var items = Enumerable.Range(0, 501)
            .Select(index =>
            {
                var item = NewDevice($"Explicit failed bulk {index}", index);
                item.Id = 90_000 + index;
                return item;
            })
            .ToList();
        items[^1].Id = items[0].Id;

        var creation = await Device.BulkCreateWithError(items, withId: true);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"devices\";");
        var all = await manager.GetAllWithError<Device>();
        var firstById = await Device.GetByIdWithError(items[0].Id);

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.False);
            Assert.That(rows.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(rows.Errors));
            Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
            Assert.That(all.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(all.Errors));
            Assert.That(all.Result, Is.Empty);
            Assert.That(firstById.Success, Is.False);
            Assert.That(firstById.Result, Is.Null);
        });
    }

    [Test]
    public async Task BulkCreate_withId_registers_canonical_instances_across_buffer_boundaries()
    {
        var manager = GenericDM.Get<Device>();
        var items = Enumerable.Range(0, 501)
            .Select(index =>
            {
                var item = NewDevice($"Explicit canonical bulk {index}", index);
                item.Id = 100_000 + index;
                item.RuntimeState = $"runtime {index}";
                return item;
            })
            .ToList();

        var creation = await Device.BulkCreateWithError(items, withId: true);
        var beforeBoundary = await Device.GetByIdWithError(items[499].Id);
        var afterBoundary = await Device.GetByIdWithError(items[500].Id);
        var all = await manager.GetAllWithError<Device>();

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(creation.Errors));
            Assert.That(beforeBoundary.Result, Is.SameAs(items[499]));
            Assert.That(afterBoundary.Result, Is.SameAs(items[500]));
            Assert.That(afterBoundary.Result!.RuntimeState, Is.EqualTo("runtime 500"));
            Assert.That(all.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(all.Errors));
            Assert.That(all.Result, Has.Count.EqualTo(501));
            Assert.That(all.Result!.Select(item => item.Id),
                Is.EquivalentTo(items.Select(item => item.Id)));
            Assert.That(all.Result!.All(item =>
                    ReferenceEquals(item, items[item.Id - 100_000])),
                Is.True);
        });
    }

    [Test]
    public async Task Update_and_delete_keep_an_already_complete_cache_consistent()
    {
        var first = await Device.Create(NewDevice("Cache mutation one", 10));
        var second = await Device.Create(NewDevice("Cache mutation two", 20));
        var secondId = second!.Id;
        var manager = GenericDM.Get<Device>();
        var complete = await manager.GetAllWithError<Device>();

        first!.Brightness = 90;
        first.RuntimeState = "must survive cache mutation";
        var update = await Device.UpdateWithError(first);
        var high = await Device.WhereWithError(device => device.Brightness >= 80);
        var low = await Device.WhereWithError(device => device.Brightness < 80);

        var deletion = await Device.DeleteWithError(second);
        var afterDeletion = await manager.GetAllWithError<Device>();
        var deletedById = await Device.GetByIdWithError(secondId);

        Assert.That(complete.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(complete.Errors));
        Assert.That(complete.Result, Has.Count.EqualTo(2));
        Assert.That(update.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(high.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(high.Errors));
        Assert.That(high.Result, Has.Count.EqualTo(1));
        Assert.That(high.Result!.Single(), Is.SameAs(first));
        Assert.That(high.Result!.Single().RuntimeState, Is.EqualTo("must survive cache mutation"));
        Assert.That(low.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(low.Errors));
        Assert.That(low.Result!.Select(device => device.Id),
            Is.EqualTo(new[] { secondId }));
        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(afterDeletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(afterDeletion.Errors));
        Assert.That(afterDeletion.Result, Has.Count.EqualTo(1));
        Assert.That(afterDeletion.Result!.Single(), Is.SameAs(first));
        Assert.That(deletedById.Success, Is.False);
        Assert.That(deletedById.Result, Is.Null);
    }

    [Test]
    public async Task Concurrent_first_where_calls_canonicalize_every_result_instance()
    {
        var insert = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"concurrent_cache_probe_records\";" +
            "INSERT INTO \"concurrent_cache_probe_records\" (\"Name\", \"Value\") VALUES ('Shared', 20);" +
            "INSERT INTO \"concurrent_cache_probe_records\" (\"Name\", \"Value\") VALUES ('Other', 5);");
        Assert.That(insert.Success, Is.True, IntegrationEnvironment.ErrorMessages(insert.Errors));

        var searches = await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => ConcurrentCacheProbeRecord.WhereWithError(item => item.Value >= 10)));
        var instances = searches
            .Select(result => result.Result!.Single())
            .ToList();
        var all = await ConcurrentCacheProbeRecord.GetAllWithError();
        var byId = await ConcurrentCacheProbeRecord.GetByIdWithError(instances[0].Id);

        Assert.Multiple(() =>
        {
            Assert.That(searches.All(result => result.Success), Is.True,
                string.Join(Environment.NewLine,
                    searches.SelectMany(result => result.Errors).Select(error => error.Message)));
            Assert.That(instances.Skip(1)
                .All(instance => ReferenceEquals(instances[0], instance)), Is.True);
            Assert.That(all.Result, Has.Count.EqualTo(2));
            Assert.That(all.Result!.Single(item => item.Name == "Shared"),
                Is.SameAs(instances[0]));
            Assert.That(byId.Result, Is.SameAs(instances[0]));
        });
    }

    [Test]
    [Explicit("Specification: StartQuery must merge selected persistent fields into the canonical cached instance.")]
    public async Task StartQuery_returns_the_canonical_cached_instance_without_losing_runtime_state()
    {
        var device = await Device.Create(NewDevice("Canonical query", 10));
        Assert.That(device, Is.Not.Null);
        device!.RuntimeState = "runtime state";
        var cached = await Device.GetByIdWithError(device.Id);

        var queried = await Device.StartQuery()
            .Where(item => item.Id == device.Id)
            .SingleWithError();

        Assert.That(cached.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(cached.Errors));
        Assert.That(queried.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(queried.Errors));
        Assert.That(queried.Result, Is.SameAs(device));
        Assert.That(queried.Result!.RuntimeState, Is.EqualTo("runtime state"));
    }

    [Test]
    public async Task Delete_rollback_restores_the_cached_instance()
    {
        var created = await Device.CreateWithError(NewDevice("Delete rollback", 10));
        Assert.That(created.Success, Is.True, IntegrationEnvironment.ErrorMessages(created.Errors));
        var instance = created.Result!;

        var manager = GenericDM.Get<Device>();
        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var deletion = await Device.DeleteWithError(instance);
            deletion.Errors.Add(new GenericError(9910, "force rollback"));
            return deletion;
        });
        Assert.That(transaction.Success, Is.False);

        var cached = await manager.GetByIdWithError<Device>(instance.Id);
        Assert.That(cached.Success, Is.True, IntegrationEnvironment.ErrorMessages(cached.Errors));
        Assert.That(cached.Result, Is.SameAs(instance));
    }

    [Test]
    public async Task Update_rollback_restores_database_and_cached_values()
    {
        var created = await Device.CreateWithError(NewDevice("Update rollback", 10));
        Assert.That(created.Success, Is.True, IntegrationEnvironment.ErrorMessages(created.Errors));
        var instance = created.Result!;

        var manager = GenericDM.Get<Device>();
        var transaction = await manager.RunInsideTransaction(async () =>
        {
            instance.Brightness = 99;
            var update = await Device.UpdateWithError(instance);
            update.Errors.Add(new GenericError(9911, "force rollback"));
            return update;
        });
        Assert.That(transaction.Success, Is.False);

        var stored = await ((DeviceManager)manager).GetByIdWithErrorNoCache<Device>(instance.Id);
        var cached = await manager.GetByIdWithError<Device>(instance.Id);
        Assert.That(stored.Success, Is.True, IntegrationEnvironment.ErrorMessages(stored.Errors));
        Assert.That(stored.Result!.Brightness, Is.EqualTo(10));
        Assert.That(cached.Result, Is.SameAs(instance));
        Assert.That(cached.Result!.Brightness, Is.EqualTo(10),
            "The cached instance must be restored to the committed database state.");
    }

    [Test]
    public async Task Update_rollback_does_not_overwrite_NotInDB_runtime_state()
    {
        var created = await Device.CreateWithError(NewDevice("Runtime rollback", 10));
        Assert.That(created.Success, Is.True, IntegrationEnvironment.ErrorMessages(created.Errors));
        var instance = created.Result!;
        instance.RuntimeState = "before transaction";

        var manager = GenericDM.Get<Device>();
        var transaction = await manager.RunInsideTransaction(async () =>
        {
            instance.Brightness = 99;
            instance.RuntimeState = "runtime changed during transaction";
            var update = await Device.UpdateWithError(instance);
            update.Errors.Add(new GenericError(9912, "force rollback"));
            return update;
        });

        Assert.That(transaction.Success, Is.False);
        Assert.That(instance.Brightness, Is.EqualTo(10),
            "Persistent values must return to the committed database state.");
        Assert.That(instance.RuntimeState, Is.EqualTo("runtime changed during transaction"),
            "Rollback must not overwrite application state that is not stored in the database.");
    }

    [Test]
    public async Task Multiple_updates_of_one_cached_instance_rollback_to_the_pre_transaction_values()
    {
        var created = await Device.CreateWithError(NewDevice("Repeated update rollback", 10));
        Assert.That(created.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(created.Errors));
        var instance = created.Result!;
        instance.RuntimeState = "runtime before updates";

        var manager = GenericDM.Get<Device>();
        var transaction = await manager.RunInsideTransaction(async () =>
        {
            instance.Brightness = 20;
            var firstUpdate = await Device.UpdateWithError(instance);
            if (!firstUpdate.Success)
                return firstUpdate;

            instance.Brightness = 30;
            instance.RuntimeState = "runtime during transaction";
            var secondUpdate = await Device.UpdateWithError(instance);
            secondUpdate.Errors.Add(new GenericError(9913, "force repeated update rollback"));
            return secondUpdate;
        });

        var stored = await ((DeviceManager)manager)
            .GetByIdWithErrorNoCache<Device>(instance.Id);
        var cached = await Device.GetByIdWithError(instance.Id);

        Assert.Multiple(() =>
        {
            Assert.That(transaction.Success, Is.False);
            Assert.That(stored.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(stored.Errors));
            Assert.That(stored.Result!.Brightness, Is.EqualTo(10));
            Assert.That(cached.Result, Is.SameAs(instance));
            Assert.That(instance.Brightness, Is.EqualTo(10),
                "Rollback callbacks must run in reverse registration order.");
            Assert.That(instance.RuntimeState, Is.EqualTo("runtime during transaction"));
        });
    }

    [Test]
    public async Task Create_then_update_rollback_removes_the_new_instance_from_database_and_cache()
    {
        var manager = GenericDM.Get<Device>();
        Device? instance = null;

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var creation = await Device.CreateWithError(
                NewDevice("Created and updated before rollback", 10));
            if (!creation.Success || creation.Result == null)
                return creation;

            instance = creation.Result;
            instance.Brightness = 40;
            instance.RuntimeState = "created during transaction";
            var update = await Device.UpdateWithError(instance);
            update.Errors.Add(new GenericError(9914, "force create-update rollback"));
            return update;
        });

        Assert.That(instance, Is.Not.Null);
        var stored = await ((DeviceManager)manager)
            .GetByIdWithErrorNoCache<Device>(instance!.Id);
        var cached = await Device.GetByIdWithError(instance.Id);

        Assert.Multiple(() =>
        {
            Assert.That(transaction.Success, Is.False);
            Assert.That(stored.Success, Is.False);
            Assert.That(stored.Result, Is.Null);
            Assert.That(cached.Success, Is.False);
            Assert.That(cached.Result, Is.Null,
                "The update rollback must run before the create rollback removes the instance.");
        });
    }

    [Test]
    public async Task Update_then_delete_rollback_restores_the_original_cached_instance_and_values()
    {
        var created = await Device.CreateWithError(
            NewDevice("Updated and deleted before rollback", 10));
        Assert.That(created.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(created.Errors));
        var instance = created.Result!;
        instance.RuntimeState = "runtime before transaction";
        var manager = GenericDM.Get<Device>();

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            instance.Brightness = 60;
            instance.RuntimeState = "runtime during transaction";
            var update = await Device.UpdateWithError(instance);
            if (!update.Success)
                return update;

            var deletion = await Device.DeleteWithError(instance);
            deletion.Errors.Add(new GenericError(9915, "force update-delete rollback"));
            return deletion;
        });

        var stored = await ((DeviceManager)manager)
            .GetByIdWithErrorNoCache<Device>(instance.Id);
        var cached = await Device.GetByIdWithError(instance.Id);

        Assert.Multiple(() =>
        {
            Assert.That(transaction.Success, Is.False);
            Assert.That(stored.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(stored.Errors));
            Assert.That(stored.Result!.Brightness, Is.EqualTo(10));
            Assert.That(cached.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(cached.Errors));
            Assert.That(cached.Result, Is.SameAs(instance));
            Assert.That(instance.Brightness, Is.EqualTo(10));
            Assert.That(instance.RuntimeState, Is.EqualTo("runtime during transaction"));
        });
    }

    private static Device NewDevice(string name, int brightness) =>
        new()
        {
            Name = name,
            Room = "Lab",
            Brightness = brightness,
            PowerConsumption = 1,
            IsOnline = true,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 1, 1)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 1, 1, 10, 0, 0))
        };
}
