using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataBulkAndEventTests
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
    public async Task Empty_bulk_create_succeeds_without_writing_rows()
    {
        var result = await Device.BulkCreateWithError([]);
        var rows = await IntegrationEnvironment.Storage.Query("SELECT COUNT(*) AS count FROM \"devices\";");

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task Bulk_create_with_ids_preserves_explicit_identifiers()
    {
        var values = new List<Device>
        {
            NewDevice(501, "First"),
            NewDevice(502, "Second")
        };

        var result = await Device.BulkCreateWithError(values, withId: true);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Id\" FROM \"devices\" ORDER BY \"Id\";");

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(rows.Result!.Select(row => row["Id"]), Is.EqualTo(new[] { "501", "502" }));
    }

    [Test]
    public async Task Invalid_item_makes_bulk_create_atomic()
    {
        var values = new List<Device>
        {
            NewDevice(0, "Valid"),
            NewDevice(0, "")
        };

        var result = await Device.BulkCreateWithError(values);
        var rows = await IntegrationEnvironment.Storage.Query("SELECT COUNT(*) AS count FROM \"devices\";");

        Assert.That(result.Success, Is.False);
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task Empty_create_update_and_delete_lists_are_valid_no_ops()
    {
        var create = await Device.CreateWithError([]);
        var update = await Device.UpdateWithError([]);
        var delete = await Device.DeleteWithError(new List<Device>());

        Assert.That(create.Success, Is.True, IntegrationEnvironment.ErrorMessages(create.Errors));
        Assert.That(create.Result, Is.Empty);
        Assert.That(update.Success, Is.True, IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(update.Result, Is.Empty);
        Assert.That(delete.Success, Is.True, IntegrationEnvironment.ErrorMessages(delete.Errors));
        Assert.That(delete.Result, Is.Empty);
    }

    [Test]
    public async Task Invalid_item_makes_list_update_atomic()
    {
        var first = (await Device.Create(NewDevice(0, "First update")))!;
        var second = (await Device.Create(NewDevice(0, "Second update")))!;
        first.Brightness = 99;
        second.Name = "";

        var update = await Device.UpdateWithError([first, second]);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Name\", \"Brightness\" FROM \"devices\" ORDER BY \"Id\";");

        Assert.That(update.Success, Is.False);
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result![0]["Brightness"], Is.EqualTo("10"));
        Assert.That(rows.Result[1]["Brightness"], Is.EqualTo("10"));
    }

    [Test]
    public async Task List_delete_is_idempotent_when_one_item_is_already_missing()
    {
        var existing = (await Device.Create(NewDevice(0, "Keep after rollback")))!;
        var missing = NewDevice(existing.Id + 100_000, "Missing");

        var deletion = await Device.DeleteWithError([existing, missing]);
        var rows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"devices\" WHERE \"Id\" = {existing.Id};");

        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(deletion.Result!.Select(item => item.Id),
            Does.Contain(existing.Id));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task Manager_events_publish_successful_create_update_and_delete_results()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var createdEvents = new List<ResultWithError<List<Device>>>();
        var updatedEvents = new List<ResultWithError<List<Device>>>();
        var deletedEvents = new List<ResultWithError<List<Device>>>();

        void OnCreated(ResultWithError<List<Device>> result) => createdEvents.Add(result);
        void OnUpdated(ResultWithError<List<Device>> result) => updatedEvents.Add(result);
        void OnDeleted(ResultWithError<List<Device>> result) => deletedEvents.Add(result);

        manager.OnCreated += OnCreated;
        manager.OnUpdated += OnUpdated;
        manager.OnDeleted += OnDeleted;
        try
        {
            var item = await Device.Create(NewDevice(0, "Events"));
            item!.Brightness = 80;
            await item.Update();
            await item.Delete();
        }
        finally
        {
            manager.OnCreated -= OnCreated;
            manager.OnUpdated -= OnUpdated;
            manager.OnDeleted -= OnDeleted;
        }

        Assert.Multiple(() =>
        {
            Assert.That(createdEvents, Has.Count.EqualTo(1));
            Assert.That(updatedEvents, Has.Count.EqualTo(1));
            Assert.That(deletedEvents, Has.Count.EqualTo(1));
            Assert.That(createdEvents[0].Success, Is.True);
            Assert.That(updatedEvents[0].Success, Is.True);
            Assert.That(deletedEvents[0].Success, Is.True);
        });
    }

    [Test]
    public async Task Failed_storage_validation_publishes_one_failed_created_event()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var createdEvents = new List<ResultWithError<List<Device>>>();

        void OnCreated(ResultWithError<List<Device>> result) => createdEvents.Add(result);
        manager.OnCreated += OnCreated;
        try
        {
            var creation = await Device.CreateWithError(NewDevice(0, ""));

            Assert.That(creation.Success, Is.False);
            Assert.That(createdEvents, Has.Count.EqualTo(1));
            Assert.That(createdEvents[0].Success, Is.False);
            Assert.That(createdEvents[0].Result, Is.Null.Or.Empty);
            Assert.That(createdEvents[0].Errors, Is.Not.Empty);
        }
        finally
        {
            manager.OnCreated -= OnCreated;
        }
    }

    [Test]
    public async Task List_create_publishes_one_aggregated_created_event()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var createdEvents = new List<ResultWithError<List<Device>>>();

        void OnCreated(ResultWithError<List<Device>> result) => createdEvents.Add(result);
        manager.OnCreated += OnCreated;
        try
        {
            var creation = await Device.CreateWithError(
                [NewDevice(0, "Batch event one"), NewDevice(0, "Batch event two")]);

            Assert.That(creation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(creation.Errors));
            Assert.That(createdEvents, Has.Count.EqualTo(1));
            Assert.That(createdEvents[0].Success, Is.True);
            Assert.That(createdEvents[0].Result, Has.Count.EqualTo(2));
            Assert.That(createdEvents[0].Result!.Select(item => item.Name),
                Is.EqualTo(new[] { "Batch event one", "Batch event two" }));
        }
        finally
        {
            manager.OnCreated -= OnCreated;
        }
    }

    [Test]
    public async Task Throwing_event_handler_does_not_fail_commit_or_block_other_handlers()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var throwingHandlerCalled = false;
        var observedEvents = new List<ResultWithError<List<Device>>>();

        void ThrowingHandler(ResultWithError<List<Device>> result)
        {
            throwingHandlerCalled = true;
            throw new InvalidOperationException("event handler exception");
        }
        void ObservingHandler(ResultWithError<List<Device>> result) =>
            observedEvents.Add(result);

        manager.OnCreated += ThrowingHandler;
        manager.OnCreated += ObservingHandler;
        ResultWithError<Device> creation = null!;
        try
        {
            creation = await Device.CreateWithError(NewDevice(0, "Throwing event"));
        }
        finally
        {
            manager.OnCreated -= ThrowingHandler;
            manager.OnCreated -= ObservingHandler;
        }
        var stored = await ((DeviceManager)manager)
            .GetByIdWithErrorNoCache<Device>(creation.Result!.Id);

        Assert.That(throwingHandlerCalled, Is.True);
        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(observedEvents, Has.Count.EqualTo(1));
        Assert.That(observedEvents[0].Success, Is.True);
        Assert.That(stored.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(stored.Errors));
        Assert.That(stored.Result, Is.Not.Null);
    }

    [Test]
    public async Task Throwing_update_and_delete_handlers_do_not_fail_commits_or_block_other_handlers()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var item = (await Device.Create(NewDevice(0, "Event isolation")))!;
        var observedUpdates = new List<ResultWithError<List<Device>>>();
        var observedDeletes = new List<ResultWithError<List<Device>>>();
        var throwingUpdateCalled = false;
        var throwingDeleteCalled = false;

        void ThrowingUpdate(ResultWithError<List<Device>> result)
        {
            throwingUpdateCalled = true;
            throw new InvalidOperationException("update handler exception");
        }
        void ThrowingDelete(ResultWithError<List<Device>> result)
        {
            throwingDeleteCalled = true;
            throw new InvalidOperationException("delete handler exception");
        }
        void ObserveUpdate(ResultWithError<List<Device>> result) => observedUpdates.Add(result);
        void ObserveDelete(ResultWithError<List<Device>> result) => observedDeletes.Add(result);

        manager.OnUpdated += ThrowingUpdate;
        manager.OnUpdated += ObserveUpdate;
        manager.OnDeleted += ThrowingDelete;
        manager.OnDeleted += ObserveDelete;
        ResultWithError<Device> update = null!;
        ResultWithError<Device> deletion = null!;
        try
        {
            item.Brightness = 90;
            update = await Device.UpdateWithError(item);
            deletion = await Device.DeleteWithError(item);
        }
        finally
        {
            manager.OnUpdated -= ThrowingUpdate;
            manager.OnUpdated -= ObserveUpdate;
            manager.OnDeleted -= ThrowingDelete;
            manager.OnDeleted -= ObserveDelete;
        }
        var storedRows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"devices\" WHERE \"Id\" = {item.Id};");

        Assert.Multiple(() =>
        {
            Assert.That(update.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(update.Errors));
            Assert.That(deletion.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(deletion.Errors));
            Assert.That(throwingUpdateCalled, Is.True);
            Assert.That(throwingDeleteCalled, Is.True);
            Assert.That(observedUpdates, Has.Count.EqualTo(1));
            Assert.That(observedUpdates[0].Result, Has.Count.EqualTo(1));
            Assert.That(observedUpdates[0].Result![0], Is.SameAs(item));
            Assert.That(observedDeletes, Has.Count.EqualTo(1));
            Assert.That(observedDeletes[0].Result, Has.Count.EqualTo(1));
            Assert.That(observedDeletes[0].Result![0], Is.SameAs(item));
            Assert.That(storedRows.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(storedRows.Errors));
            Assert.That(storedRows.Result!.Single()["count"], Is.EqualTo("0"));
        });
    }

    [Test]
    public async Task Failed_update_validation_publishes_one_failed_event_without_changing_storage()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var item = (await Device.Create(NewDevice(0, "Valid before update")))!;
        var updatedEvents = new List<ResultWithError<List<Device>>>();

        void OnUpdated(ResultWithError<List<Device>> result) => updatedEvents.Add(result);
        manager.OnUpdated += OnUpdated;
        ResultWithError<Device> update;
        try
        {
            item.Name = "";
            update = await Device.UpdateWithError(item);
        }
        finally
        {
            manager.OnUpdated -= OnUpdated;
        }
        var stored = await manager.GetByIdWithErrorNoCache<Device>(item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(update.Success, Is.False);
            Assert.That(updatedEvents, Has.Count.EqualTo(1));
            Assert.That(updatedEvents[0].Success, Is.False);
            Assert.That(updatedEvents[0].Errors, Is.Not.Empty);
            Assert.That(stored.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(stored.Errors));
            Assert.That(stored.Result!.Name, Is.EqualTo("Valid before update"));
        });
    }

    [Test]
    public async Task Failed_delete_publishes_one_failed_event_and_preserves_database_and_cache()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var item = (await Device.Create(NewDevice(0, "Protected deletion")))!;
        var deletedEvents = new List<ResultWithError<List<Device>>>();
        var triggerName = $"prevent_device_delete_{item.Id}";

        var createTrigger = await IntegrationEnvironment.Storage.Execute(
            $"CREATE TRIGGER \"{triggerName}\" BEFORE DELETE ON \"devices\" " +
            $"WHEN OLD.\"Id\" = {item.Id} " +
            "BEGIN SELECT RAISE(ABORT, 'protected device'); END;");
        Assert.That(createTrigger.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(createTrigger.Errors));

        void OnDeleted(ResultWithError<List<Device>> result) => deletedEvents.Add(result);
        manager.OnDeleted += OnDeleted;
        ResultWithError<Device> deletion;
        try
        {
            deletion = await Device.DeleteWithError(item);
        }
        finally
        {
            manager.OnDeleted -= OnDeleted;
            var dropTrigger = await IntegrationEnvironment.Storage.Execute(
                $"DROP TRIGGER IF EXISTS \"{triggerName}\";");
            Assert.That(dropTrigger.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(dropTrigger.Errors));
        }

        var stored = await manager.GetByIdWithErrorNoCache<Device>(item.Id);
        var cached = await Device.GetByIdWithError(item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(deletion.Success, Is.False);
            Assert.That(deletedEvents, Has.Count.EqualTo(1));
            Assert.That(deletedEvents[0].Success, Is.False);
            Assert.That(deletedEvents[0].Errors, Is.Not.Empty);
            Assert.That(stored.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(stored.Errors));
            Assert.That(stored.Result!.Name, Is.EqualTo("Protected deletion"));
            Assert.That(cached.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(cached.Errors));
            Assert.That(cached.Result, Is.SameAs(item));
        });
    }

    [Test]
    public async Task Storage_failure_on_second_list_create_rolls_back_and_publishes_one_failed_event()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var createdEvents = new List<ResultWithError<List<Device>>>();
        var triggerName = "reject_second_device_create";
        var first = NewDevice(0, "Accepted before rollback");
        var second = NewDevice(0, "Rejected by storage");

        var createTrigger = await IntegrationEnvironment.Storage.Execute(
            $"CREATE TRIGGER \"{triggerName}\" BEFORE INSERT ON \"devices\" " +
            "WHEN NEW.\"Name\" = 'Rejected by storage' " +
            "BEGIN SELECT RAISE(ABORT, 'rejected device'); END;");
        Assert.That(createTrigger.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(createTrigger.Errors));

        void OnCreated(ResultWithError<List<Device>> result) => createdEvents.Add(result);
        manager.OnCreated += OnCreated;
        ResultWithError<List<Device>> creation;
        try
        {
            creation = await Device.CreateWithError([first, second]);
        }
        finally
        {
            manager.OnCreated -= OnCreated;
            var dropTrigger = await IntegrationEnvironment.Storage.Execute(
                $"DROP TRIGGER IF EXISTS \"{triggerName}\";");
            Assert.That(dropTrigger.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(dropTrigger.Errors));
        }

        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"devices\";");
        var firstCached = first.Id <= 0
            ? null
            : await Device.GetByIdWithError(first.Id);

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.False);
            Assert.That(createdEvents, Has.Count.EqualTo(1));
            Assert.That(createdEvents[0].Success, Is.False);
            Assert.That(createdEvents[0].Errors, Is.Not.Empty);
            Assert.That(rows.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(rows.Errors));
            Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
            if (firstCached != null)
            {
                Assert.That(firstCached.Success, Is.False);
                Assert.That(firstCached.Result, Is.Null);
            }
        });
    }

    [Test]
    public async Task Storage_failure_on_second_list_update_restores_cache_and_publishes_one_failed_event()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var first = (await Device.Create(NewDevice(0, "First before update")))!;
        var second = (await Device.Create(NewDevice(0, "Second before update")))!;
        var updatedEvents = new List<ResultWithError<List<Device>>>();
        var triggerName = "reject_second_device_update";

        var createTrigger = await IntegrationEnvironment.Storage.Execute(
            $"CREATE TRIGGER \"{triggerName}\" BEFORE UPDATE ON \"devices\" " +
            "WHEN NEW.\"Name\" = 'Rejected update' " +
            "BEGIN SELECT RAISE(ABORT, 'rejected update'); END;");
        Assert.That(createTrigger.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(createTrigger.Errors));

        first.Brightness = 91;
        second.Name = "Rejected update";
        void OnUpdated(ResultWithError<List<Device>> result) => updatedEvents.Add(result);
        manager.OnUpdated += OnUpdated;
        ResultWithError<List<Device>> update;
        try
        {
            update = await Device.UpdateWithError([first, second]);
        }
        finally
        {
            manager.OnUpdated -= OnUpdated;
            var dropTrigger = await IntegrationEnvironment.Storage.Execute(
                $"DROP TRIGGER IF EXISTS \"{triggerName}\";");
            Assert.That(dropTrigger.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(dropTrigger.Errors));
        }

        var firstStored = await manager.GetByIdWithErrorNoCache<Device>(first.Id);
        var secondStored = await manager.GetByIdWithErrorNoCache<Device>(second.Id);
        var firstCached = await Device.GetByIdWithError(first.Id);
        var secondCached = await Device.GetByIdWithError(second.Id);

        Assert.Multiple(() =>
        {
            Assert.That(update.Success, Is.False);
            Assert.That(updatedEvents, Has.Count.EqualTo(1));
            Assert.That(updatedEvents[0].Success, Is.False);
            Assert.That(updatedEvents[0].Errors, Is.Not.Empty);
            Assert.That(firstStored.Result!.Brightness, Is.EqualTo(10));
            Assert.That(secondStored.Result!.Name, Is.EqualTo("Second before update"));
            Assert.That(firstCached.Result, Is.SameAs(first));
            Assert.That(secondCached.Result, Is.SameAs(second));
            Assert.That(first.Brightness, Is.EqualTo(10));
            Assert.That(second.Name, Is.EqualTo("Second before update"));
        });
    }

    [Test]
    public async Task Storage_failure_on_second_list_delete_restores_cache_and_publishes_one_failed_event()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var first = (await Device.Create(NewDevice(0, "First before delete")))!;
        var second = (await Device.Create(NewDevice(0, "Protected second delete")))!;
        var deletedEvents = new List<ResultWithError<List<Device>>>();
        var triggerName = "reject_second_device_delete";

        var createTrigger = await IntegrationEnvironment.Storage.Execute(
            $"CREATE TRIGGER \"{triggerName}\" BEFORE DELETE ON \"devices\" " +
            $"WHEN OLD.\"Id\" = {second.Id} " +
            "BEGIN SELECT RAISE(ABORT, 'rejected delete'); END;");
        Assert.That(createTrigger.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(createTrigger.Errors));

        void OnDeleted(ResultWithError<List<Device>> result) => deletedEvents.Add(result);
        manager.OnDeleted += OnDeleted;
        ResultWithError<List<Device>> deletion;
        try
        {
            deletion = await Device.DeleteWithError([first, second]);
        }
        finally
        {
            manager.OnDeleted -= OnDeleted;
            var dropTrigger = await IntegrationEnvironment.Storage.Execute(
                $"DROP TRIGGER IF EXISTS \"{triggerName}\";");
            Assert.That(dropTrigger.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(dropTrigger.Errors));
        }

        var firstStored = await manager.GetByIdWithErrorNoCache<Device>(first.Id);
        var secondStored = await manager.GetByIdWithErrorNoCache<Device>(second.Id);
        var firstCached = await Device.GetByIdWithError(first.Id);
        var secondCached = await Device.GetByIdWithError(second.Id);

        Assert.Multiple(() =>
        {
            Assert.That(deletion.Success, Is.False);
            Assert.That(deletedEvents, Has.Count.EqualTo(1));
            Assert.That(deletedEvents[0].Success, Is.False);
            Assert.That(deletedEvents[0].Errors, Is.Not.Empty);
            Assert.That(firstStored.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(firstStored.Errors));
            Assert.That(secondStored.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(secondStored.Errors));
            Assert.That(firstCached.Result, Is.SameAs(first));
            Assert.That(secondCached.Result, Is.SameAs(second));
        });
    }

    [Test]
    [Explicit("Specification: successful CRUD events are currently published before an outer transaction commits.")]
    public async Task Rolled_back_transaction_does_not_publish_success_event()
    {
        var manager = (DeviceManager)GenericDM.Get<Device>();
        var createdEvents = new List<ResultWithError<List<Device>>>();

        void OnCreated(ResultWithError<List<Device>> result) => createdEvents.Add(result);
        manager.OnCreated += OnCreated;
        try
        {
            var transaction = await manager.RunInsideTransaction(async () =>
            {
                var creation = await Device.CreateWithError(
                    NewDevice(0, "Rolled back event"));
                creation.Errors.Add(new GenericError(9940, "force event rollback"));
                return creation;
            });

            Assert.That(transaction.Success, Is.False);
            Assert.That(createdEvents, Is.Empty);
        }
        finally
        {
            manager.OnCreated -= OnCreated;
        }
    }

    private static Device NewDevice(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Room = "Lab",
            Brightness = 10,
            PowerConsumption = 1,
            IsOnline = true,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 1, 1)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 1, 1, 10, 0, 0))
        };
}
