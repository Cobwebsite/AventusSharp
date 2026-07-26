using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataInheritanceTests
{
    [SetUp]
    public async Task ClearActuators()
    {
        var manager = GenericDM.Get<ITestActuator>();
        var cached = await manager.GetAllWithError<ITestActuator>();
        if (cached.Result is { Count: > 0 })
        {
            ((IDatabaseDM)manager).RemoveRecordsItems(cached.Result);
        }

        var result = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_forced_asset_bindings\";" +
            "DELETE FROM \"test_forced_gateways\";" +
            "DELETE FROM \"test_forced_cameras\";" +
            "DELETE FROM \"test_forced_speakers\";" +
            "DELETE FROM \"test_dimmers\";" +
            "DELETE FROM \"test_relays\";" +
            "DELETE FROM \"test_actuators\";");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Generic_parent_uses_one_table_and_restores_concrete_types()
    {
        var dimmer = new TestDimmer { Name = "Ceiling", Level = 42 };
        var relay = new TestRelay { Name = "Pump", IsClosed = true };

        var dimmerCreated = await TestDimmer.CreateWithError(dimmer);
        var relayCreated = await TestRelay.CreateWithError(relay);
        var manager = GenericDM.Get<ITestActuator>();
        var loaded = await manager.GetAllWithError<ITestActuator>();

        Assert.Multiple(() =>
        {
            Assert.That(dimmerCreated.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(dimmerCreated.Errors));
            Assert.That(relayCreated.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(relayCreated.Errors));
            Assert.That(loaded.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(loaded.Errors));
            Assert.That(loaded.Result, Has.Count.EqualTo(2));
            Assert.That(loaded.Result, Has.Exactly(1).TypeOf<TestDimmer>());
            Assert.That(loaded.Result, Has.Exactly(1).TypeOf<TestRelay>());
            Assert.That(loaded.Result!.OfType<TestDimmer>().Single().Level, Is.EqualTo(42));
            Assert.That(loaded.Result!.OfType<TestRelay>().Single().IsClosed, Is.True);
        });
    }

    [Test]
    public async Task Type_specific_query_only_returns_the_requested_child_type()
    {
        await TestDimmer.Create(new TestDimmer { Name = "Dimmer", Level = 10 });
        await TestRelay.Create(new TestRelay { Name = "Relay", IsClosed = false });

        var dimmers = await TestDimmer.WhereWithError(item => item.Level >= 10);
        var relays = await TestRelay.GetAllWithError();

        Assert.That(dimmers.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(dimmers.Errors));
        Assert.That(dimmers.Result!.Select(item => item.Name), Is.EqualTo(new[] { "Dimmer" }));
        Assert.That(relays.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(relays.Errors));
        Assert.That(relays.Result!.Select(item => item.Name), Is.EqualTo(new[] { "Relay" }));
    }

    [Test]
    [Explicit("Specification: BulkCreate does not yet coordinate parent and child inheritance tables.")]
    public async Task BulkCreate_withId_preserves_canonical_children_in_the_shared_parent_cache()
    {
        var dimmer = new TestDimmer
        {
            Id = 120_001,
            Name = "Bulk inherited dimmer",
            Level = 45
        };
        var relay = new TestRelay
        {
            Id = 120_002,
            Name = "Bulk inherited relay",
            IsClosed = true
        };

        var dimmerCreation = await TestDimmer.BulkCreateWithError(
            [dimmer], withId: true);
        var relayCreation = await TestRelay.BulkCreateWithError(
            [relay], withId: true);
        var manager = GenericDM.Get<ITestActuator>();
        var dimmerById = await manager.GetByIdWithError<TestDimmer>(dimmer.Id);
        var relayById = await manager.GetByIdWithError<TestRelay>(relay.Id);
        var all = await manager.GetAllWithError<ITestActuator>();

        Assert.Multiple(() =>
        {
            Assert.That(dimmerCreation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(dimmerCreation.Errors));
            Assert.That(relayCreation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(relayCreation.Errors));
            Assert.That(dimmerById.Result, Is.SameAs(dimmer));
            Assert.That(relayById.Result, Is.SameAs(relay));
            Assert.That(all.Result!.Single(item => item.Id == dimmer.Id),
                Is.SameAs(dimmer));
            Assert.That(all.Result!.Single(item => item.Id == relay.Id),
                Is.SameAs(relay));
        });
    }

    [Test]
    public async Task Generated_schema_separates_parent_and_child_members()
    {
        var parentColumns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('test_actuators');");
        var dimmerColumns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('test_dimmers');");
        var relayColumns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('test_relays');");

        Assert.That(parentColumns.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(parentColumns.Errors));
        Assert.That(dimmerColumns.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(dimmerColumns.Errors));
        Assert.That(relayColumns.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(relayColumns.Errors));
        Assert.That(parentColumns.Result!.Select(column => column["name"]), Is.SupersetOf(new[]
        {
            "Id",
            "Name",
            "__type"
        }));
        Assert.That(dimmerColumns.Result!.Select(column => column["name"]),
            Is.SupersetOf(new[] { "Id", "Level" }));
        Assert.That(relayColumns.Result!.Select(column => column["name"]),
            Is.SupersetOf(new[] { "Id", "IsClosed" }));
    }

    [Test]
    public async Task ForceInherit_pushes_business_parent_members_into_each_child_table()
    {
        var cameraColumns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('test_forced_cameras');");
        var speakerColumns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('test_forced_speakers');");
        var parentTable = await IntegrationEnvironment.Storage.Query(
            "SELECT name FROM sqlite_master " +
            "WHERE type = 'table' AND name = 'TestForcedAsset';");

        Assert.That(cameraColumns.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(cameraColumns.Errors));
        Assert.That(speakerColumns.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(speakerColumns.Errors));
        Assert.That(cameraColumns.Result!.Select(column => column["name"]),
            Is.SupersetOf(new[] { "Id", "Label", "Resolution" }));
        Assert.That(speakerColumns.Result!.Select(column => column["name"]),
            Is.SupersetOf(new[] { "Id", "Label", "Volume" }));
        Assert.That(parentTable.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(parentTable.Errors));
        Assert.That(parentTable.Result, Is.Empty,
            "A ForceInherit parent must not generate its own table.");
    }

    [Test]
    public async Task ForceInherit_children_support_crud_with_the_inherited_business_fields()
    {
        var clear = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_forced_cameras\";" +
            "DELETE FROM \"test_forced_speakers\";");
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        var camera = new TestForcedCamera { Label = "Entrance", Resolution = 4_096 };
        var speaker = new TestForcedSpeaker { Label = "Kitchen", Volume = 35 };

        var cameraCreated = await TestForcedCamera.CreateWithError(camera);
        var speakerCreated = await TestForcedSpeaker.CreateWithError(speaker);
        var loadedCamera = await TestForcedCamera.GetByIdWithError(camera.Id);
        var loadedSpeaker = await TestForcedSpeaker.GetByIdWithError(speaker.Id);

        Assert.That(cameraCreated.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(cameraCreated.Errors));
        Assert.That(speakerCreated.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(speakerCreated.Errors));
        Assert.That(loadedCamera.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loadedCamera.Errors));
        Assert.That(loadedSpeaker.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loadedSpeaker.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(loadedCamera.Result!.Label, Is.EqualTo("Entrance"));
            Assert.That(loadedCamera.Result.Resolution, Is.EqualTo(4_096));
            Assert.That(loadedSpeaker.Result!.Label, Is.EqualTo("Kitchen"));
            Assert.That(loadedSpeaker.Result.Volume, Is.EqualTo(35));
        });
    }

    [Test]
    public async Task Multiple_ForceInherit_levels_accumulate_members_in_the_concrete_table()
    {
        var columns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('test_forced_gateways');");

        Assert.That(columns.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(columns.Errors));
        Assert.That(columns.Result!.Select(column => column["name"]),
            Is.SupersetOf(new[] { "Id", "Label", "IpAddress", "PortCount" }));
    }

    [Test]
    public async Task Multiple_ForceInherit_levels_preserve_all_values_during_crud()
    {
        var clear = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_forced_gateways\";");
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        var gateway = new TestForcedGateway
        {
            Label = "Main gateway",
            IpAddress = "192.168.1.1",
            PortCount = 8
        };

        var created = await TestForcedGateway.CreateWithError(gateway);
        var loaded = await TestForcedGateway.GetByIdWithError(gateway.Id);

        Assert.That(created.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(created.Errors));
        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Result!.Label, Is.EqualTo("Main gateway"));
            Assert.That(loaded.Result.IpAddress, Is.EqualTo("192.168.1.1"));
            Assert.That(loaded.Result.PortCount, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task BulkCreate_withId_preserves_a_ForceInherit_child_as_the_canonical_instance()
    {
        var gateway = new TestForcedGateway
        {
            Id = 120_003,
            Label = "Bulk forced gateway",
            IpAddress = "10.20.30.40",
            PortCount = 16
        };

        var creation = await TestForcedGateway.BulkCreateWithError(
            [gateway], withId: true);
        var loaded = await TestForcedGateway.GetByIdWithError(gateway.Id);

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(creation.Errors));
            Assert.That(loaded.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(loaded.Errors));
            Assert.That(loaded.Result, Is.SameAs(gateway));
            Assert.That(loaded.Result!.Label, Is.EqualTo("Bulk forced gateway"));
            Assert.That(loaded.Result.IpAddress, Is.EqualTo("10.20.30.40"));
            Assert.That(loaded.Result.PortCount, Is.EqualTo(16));
        });
    }

    [Test]
    public async Task BulkCreate_withId_ForceInherit_rollback_removes_database_and_cache_entries()
    {
        var gateway = new TestForcedGateway
        {
            Id = 120_004,
            Label = "Rolled back forced gateway",
            IpAddress = "10.20.30.41",
            PortCount = 24
        };
        var manager = GenericDM.Get<TestForcedGateway>();

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var creation = await TestForcedGateway.BulkCreateWithError(
                [gateway], withId: true);
            creation.Errors.Add(new AventusSharp.Tools.GenericError(
                9923, "force ForceInherit bulk rollback"));
            return creation;
        });
        var rows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"test_forced_gateways\" " +
            $"WHERE \"Id\" = {gateway.Id};");
        var cached = await TestForcedGateway.GetByIdWithError(gateway.Id);

        Assert.Multiple(() =>
        {
            Assert.That(transaction.Success, Is.False);
            Assert.That(rows.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(rows.Errors));
            Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
            Assert.That(cached.Success, Is.False);
            Assert.That(cached.Result, Is.Null);
        });
    }

    [Test]
    public async Task Relation_to_a_ForceInherit_child_generates_a_foreign_key_to_its_concrete_table()
    {
        var columns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('test_forced_asset_bindings');");
        var foreignKeys = await IntegrationEnvironment.Storage.Query(
            "PRAGMA foreign_key_list('test_forced_asset_bindings');");

        Assert.That(columns.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(columns.Errors));
        Assert.That(foreignKeys.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(foreignKeys.Errors));
        Assert.That(columns.Result!.Select(column => column["name"]),
            Does.Contain("Gateway"));
        Assert.That(foreignKeys.Result, Has.Some.Matches<Dictionary<string, string?>>(
            row => row["from"] == "Gateway" && row["table"] == "test_forced_gateways"));
    }

    [Test]
    public async Task Relation_to_a_ForceInherit_child_is_persisted_and_reuses_the_cached_instance()
    {
        var clear = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_forced_asset_bindings\";" +
            "DELETE FROM \"test_forced_gateways\";");
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        var gateway = await TestForcedGateway.Create(new TestForcedGateway
        {
            Label = "Relation gateway",
            IpAddress = "10.0.0.1",
            PortCount = 4
        });
        Assert.That(gateway, Is.Not.Null);

        var binding = await TestForcedAssetBinding.Create(new TestForcedAssetBinding
        {
            Name = "Primary",
            Gateway = gateway!
        });
        Assert.That(binding, Is.Not.Null);

        var loaded = await TestForcedAssetBinding.GetByIdWithError(binding!.Id);

        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Gateway, Is.SameAs(gateway));
        Assert.That(loaded.Result.Gateway.Label, Is.EqualTo("Relation gateway"));
    }

    [Test]
    public async Task BulkCreate_withId_relation_to_ForceInherit_reuses_both_canonical_instances()
    {
        var gateway = new TestForcedGateway
        {
            Id = 120_005,
            Label = "Bulk relation gateway",
            IpAddress = "10.0.0.5",
            PortCount = 12
        };
        var binding = new TestForcedAssetBinding
        {
            Id = 120_006,
            Name = "Bulk relation binding",
            Gateway = gateway
        };

        var gatewayCreation = await TestForcedGateway.BulkCreateWithError(
            [gateway], withId: true);
        var bindingCreation = await TestForcedAssetBinding.BulkCreateWithError(
            [binding], withId: true);
        var cached = await TestForcedAssetBinding.GetByIdWithError(binding.Id);
        var noCache = await ((SimpleDatabaseDM<TestForcedAssetBinding>)
                GenericDM.Get<TestForcedAssetBinding>())
            .GetByIdWithErrorNoCache<TestForcedAssetBinding>(binding.Id);
        var raw = await IntegrationEnvironment.Storage.Query(
            $"SELECT \"Gateway\" FROM \"test_forced_asset_bindings\" " +
            $"WHERE \"Id\" = {binding.Id};");

        Assert.Multiple(() =>
        {
            Assert.That(gatewayCreation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(gatewayCreation.Errors));
            Assert.That(bindingCreation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(bindingCreation.Errors));
            Assert.That(raw.Result!.Single()["Gateway"], Is.EqualTo(gateway.Id.ToString()));
            Assert.That(cached.Result, Is.SameAs(binding));
            Assert.That(cached.Result!.Gateway, Is.SameAs(gateway));
            Assert.That(noCache.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(noCache.Errors));
            Assert.That(noCache.Result!.Gateway, Is.SameAs(gateway));
        });
    }

    [Test]
    public void ForceInherit_parent_interface_does_not_get_a_standalone_manager()
    {
        Assert.That(
            () => GenericDM.Get<ITestForcedAsset>(),
            Throws.TypeOf<AventusSharp.Tools.AventusException>()
                .With.Message.Contains("Can't found a data manger for type ITestForcedAsset"));
    }
}
