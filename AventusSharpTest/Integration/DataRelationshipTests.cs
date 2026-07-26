using AventusSharpTest.Integration.Models;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataRelationshipTests
{
    [SetUp]
    public async Task ClearTables()
    {
        var result = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_scenes_test_lamps\";" +
            "DELETE FROM \"test_scenes\";" +
            "DELETE FROM \"test_sensors\";" +
            "DELETE FROM \"test_lazy_links\";" +
            "DELETE FROM \"test_lamps\";" +
            "DELETE FROM \"test_owners\";" +
            "DELETE FROM \"test_owned_profiles\";" +
            "DELETE FROM \"test_rooms\";");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task One_to_many_link_is_persisted_and_loaded()
    {
        var room = await TestRoom.Create(new TestRoom { Name = "Kitchen" });
        Assert.That(room, Is.Not.Null);

        var lamp = await TestLamp.Create(new TestLamp { Name = "Ceiling", Room = room! });
        Assert.That(lamp, Is.Not.Null);

        var loaded = await ((TestLampManager)AventusSharp.Data.Manager.GenericDM.Get<TestLamp>())
            .GetByIdWithErrorNoCache<TestLamp>(lamp!.Id);

        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result, Is.Not.Null);
        Assert.That(loaded.Result!.Room.Id, Is.EqualTo(room!.Id));
    }

    [Test]
    public async Task Direct_relation_can_be_reassigned_and_cleared_when_nullable()
    {
        var firstRoom = await TestRoom.Create(new TestRoom
        {
            Name = "Sensor room one",
            Code = "sensor-one"
        });
        var secondRoom = await TestRoom.Create(new TestRoom
        {
            Name = "Sensor room two",
            Code = "sensor-two"
        });
        var sensor = await TestSensor.Create(new TestSensor
        {
            Name = "Movable sensor",
            Room = firstRoom
        });

        sensor!.Room = secondRoom;
        var reassignment = await TestSensor.UpdateWithError(sensor);
        var reassigned = await ((TestSensorManager)GenericDM.Get<TestSensor>())
            .GetByIdWithErrorNoCache<TestSensor>(sensor.Id);

        sensor.Room = null;
        var clearing = await TestSensor.UpdateWithError(sensor);
        var cleared = await ((TestSensorManager)GenericDM.Get<TestSensor>())
            .GetByIdWithErrorNoCache<TestSensor>(sensor.Id);

        Assert.That(reassignment.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(reassignment.Errors));
        Assert.That(reassigned.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(reassigned.Errors));
        Assert.That(reassigned.Result!.Room!.Id, Is.EqualTo(secondRoom!.Id));
        Assert.That(clearing.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(clearing.Errors));
        Assert.That(cleared.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(cleared.Errors));
        Assert.That(cleared.Result!.Room, Is.Null);
    }

    [Test]
    public async Task Invalid_direct_relation_update_preserves_the_previous_foreign_key()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Preserved direct room",
            Code = "preserved-direct"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Preserved direct lamp",
            Room = room!
        });

        lamp!.Room = TestRoom.OnlyId(999_999);
        var update = await TestLamp.UpdateWithError(lamp);
        var loaded = await ((TestLampManager)GenericDM.Get<TestLamp>())
            .GetByIdWithErrorNoCache<TestLamp>(lamp.Id);

        Assert.That(update.Success, Is.False);
        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Room.Id, Is.EqualTo(room!.Id));
    }

    [Test]
    public async Task Invalid_direct_relation_rolls_back_all_bulk_updates_and_cached_instances()
    {
        var originalRoom = await TestRoom.Create(new TestRoom
        {
            Name = "Bulk direct original",
            Code = "bulk-direct-original"
        });
        var replacementRoom = await TestRoom.Create(new TestRoom
        {
            Name = "Bulk direct replacement",
            Code = "bulk-direct-replacement"
        });
        var firstLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk direct one",
            Room = originalRoom!
        });
        var secondLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk direct two",
            Room = originalRoom!
        });
        var firstCached = await TestLamp.GetByIdWithError(firstLamp!.Id);
        var secondCached = await TestLamp.GetByIdWithError(secondLamp!.Id);

        firstLamp.Room = replacementRoom!;
        secondLamp.Room = TestRoom.OnlyId(999_999);
        var update = await TestLamp.UpdateWithError([firstLamp, secondLamp]);
        var firstStored = await ((TestLampManager)GenericDM.Get<TestLamp>())
            .GetByIdWithErrorNoCache<TestLamp>(firstLamp.Id);
        var secondStored = await ((TestLampManager)GenericDM.Get<TestLamp>())
            .GetByIdWithErrorNoCache<TestLamp>(secondLamp.Id);

        Assert.That(firstCached.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(firstCached.Errors));
        Assert.That(secondCached.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondCached.Errors));
        Assert.That(update.Success, Is.False);
        Assert.That(firstStored.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(firstStored.Errors));
        Assert.That(firstStored.Result!.Room.Id, Is.EqualTo(originalRoom!.Id));
        Assert.That(secondStored.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondStored.Errors));
        Assert.That(secondStored.Result!.Room.Id, Is.EqualTo(originalRoom.Id));
        Assert.That(firstLamp.Room, Is.SameAs(originalRoom));
        Assert.That(secondLamp.Room, Is.SameAs(originalRoom));
    }

    [Test]
    public async Task Foreign_key_failure_in_a_create_list_rolls_back_database_and_cache()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Valid parent",
            Code = "valid-parent"
        });
        var valid = new TestLamp { Name = "Valid child", Room = room! };
        var invalid = new TestLamp
        {
            Name = "Invalid child",
            Room = TestRoom.OnlyId(room!.Id + 100_000)
        };

        var creation = await TestLamp.CreateWithError([valid, invalid]);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_lamps\";");
        var cached = await TestLamp.GetByIdWithError(valid.Id);

        Assert.That(creation.Success, Is.False);
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(cached.Success, Is.False);
        Assert.That(cached.Result, Is.Null);
    }

    [Test]
    public async Task Prepared_contains_converts_a_storable_collection_to_foreign_key_ids()
    {
        var firstRoom = await TestRoom.Create(new TestRoom
        {
            Name = "Prepared room one",
            Code = "prepared-one"
        });
        var secondRoom = await TestRoom.Create(new TestRoom
        {
            Name = "Prepared room two",
            Code = "prepared-two"
        });
        var firstLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Prepared lamp one",
            Room = firstRoom!
        });
        await TestLamp.Create(new TestLamp
        {
            Name = "Prepared lamp two",
            Room = secondRoom!
        });
        var rooms = new List<TestRoom>();
        var prepared = TestLamp.StartQuery()
            .WhereWithParameters(lamp => rooms.Contains(lamp.Room));

        var result = await prepared.New()
            .Prepare(new List<TestRoom> { firstRoom! })
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Id),
            Is.EqualTo(new[] { firstLamp!.Id }));
    }

    [Test]
    public async Task Many_to_many_link_uses_generated_intermediate_table()
    {
        var room = await TestRoom.Create(new TestRoom { Name = "Living room" });
        var first = await TestLamp.Create(new TestLamp { Name = "Floor", Room = room! });
        var second = await TestLamp.Create(new TestLamp { Name = "Wall", Room = room! });

        var scene = await TestScene.Create(new TestScene
        {
            Name = "Evening",
            Lamps = [first!, second!]
        });
        Assert.That(scene, Is.Not.Null);

        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("2"));

        var loaded = await ((TestSceneManager)AventusSharp.Data.Manager.GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(scene!.Id);
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Lamps.Select(item => item.Id),
            Is.EquivalentTo(new[] { first!.Id, second!.Id }));
    }

    [Test]
    [Explicit("Specification: LambdaTranslator does not yet query a many-to-many collection member with Contains.")]
    public async Task Many_to_many_collection_can_be_filtered_with_contains_and_its_negation()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Filter room",
            Code = "filter-room"
        });
        var firstLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Filter lamp one",
            Room = room!
        });
        var secondLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Filter lamp two",
            Room = room!
        });
        var matching = await TestScene.Create(new TestScene
        {
            Name = "Matching scene",
            Lamps = [firstLamp!]
        });
        var other = await TestScene.Create(new TestScene
        {
            Name = "Other scene",
            Lamps = [secondLamp!]
        });
        var empty = await TestScene.Create(new TestScene
        {
            Name = "Empty scene",
            Lamps = []
        });

        var contains = await TestScene.StartQuery()
            .Where(scene => scene.Lamps.Contains(firstLamp!))
            .RunWithError();
        var doesNotContain = await TestScene.StartQuery()
            .Where(scene => !scene.Lamps.Contains(firstLamp!))
            .RunWithError();

        Assert.That(contains.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(contains.Errors));
        Assert.That(contains.Result!.Select(scene => scene.Id),
            Is.EqualTo(new[] { matching!.Id }));
        Assert.That(doesNotContain.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(doesNotContain.Errors));
        Assert.That(doesNotContain.Result!.Select(scene => scene.Id),
            Is.EquivalentTo(new[] { other!.Id, empty!.Id }));
    }

    [Test]
    public async Task Many_to_many_empty_collection_round_trips_as_an_empty_list()
    {
        var scene = await TestScene.Create(new TestScene
        {
            Name = "Empty relation",
            Lamps = []
        });

        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");
        var loaded = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(scene!.Id);

        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Lamps, Is.Not.Null);
        Assert.That(loaded.Result.Lamps, Is.Empty);
    }

    [Test]
    public async Task Many_to_many_update_replaces_then_clears_intermediate_rows()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Update relation room",
            Code = "update-relation"
        });
        var first = await TestLamp.Create(new TestLamp
        {
            Name = "Initial relation",
            Room = room!
        });
        var second = await TestLamp.Create(new TestLamp
        {
            Name = "Replacement relation",
            Room = room!
        });
        var scene = await TestScene.Create(new TestScene
        {
            Name = "Updated scene",
            Lamps = [first!]
        });

        scene!.Lamps = [second!];
        var replacement = await TestScene.UpdateWithError(scene);
        var afterReplacement = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(scene.Id);

        scene.Lamps = [];
        var clearing = await TestScene.UpdateWithError(scene);
        var afterClearing = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(scene.Id);
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(replacement.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(replacement.Errors));
        Assert.That(afterReplacement.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(afterReplacement.Errors));
        Assert.That(afterReplacement.Result!.Lamps.Select(lamp => lamp.Id),
            Is.EqualTo(new[] { second!.Id }));
        Assert.That(clearing.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(clearing.Errors));
        Assert.That(afterClearing.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(afterClearing.Errors));
        Assert.That(afterClearing.Result!.Lamps, Is.Empty);
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task Deleting_many_to_many_owner_removes_its_intermediate_rows()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Delete relation room",
            Code = "delete-relation"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Deleted relation",
            Room = room!
        });
        var scene = await TestScene.Create(new TestScene
        {
            Name = "Deleted scene",
            Lamps = [lamp!]
        });

        var deletion = await TestScene.DeleteWithError(scene!);
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");
        var lampStillExists = await ((TestLampManager)GenericDM.Get<TestLamp>())
            .GetByIdWithErrorNoCache<TestLamp>(lamp!.Id);

        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(lampStillExists.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(lampStillExists.Errors));
        Assert.That(lampStillExists.Result, Is.Not.Null,
            "Deleting the relation owner must not delete the linked item.");
    }

    [Test]
    public async Task Many_to_many_duplicate_items_create_a_single_intermediate_link()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Duplicate relation room",
            Code = "duplicate-relation"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Duplicate relation lamp",
            Room = room!
        });

        var creation = await TestScene.CreateWithError(new TestScene
        {
            Name = "Deduplicated scene",
            Lamps = [lamp!, lamp!]
        });
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("1"));
    }

    [Test]
    public async Task Invalid_many_to_many_link_rolls_back_owner_creation()
    {
        var invalidLamp = TestLamp.OnlyId(999_999);

        var creation = await TestScene.CreateWithError(new TestScene
        {
            Name = "Invalid relation scene",
            Lamps = [invalidLamp]
        });
        var owners = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes\";");
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(creation.Success, Is.False);
        Assert.That(owners.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(owners.Errors));
        Assert.That(owners.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task Invalid_many_to_many_update_preserves_the_previous_links()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Rollback relation room",
            Code = "rollback-relation"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Preserved relation",
            Room = room!
        });
        var scene = await TestScene.Create(new TestScene
        {
            Name = "Rollback scene",
            Lamps = [lamp!]
        });

        scene!.Lamps = [TestLamp.OnlyId(999_999)];
        var update = await TestScene.UpdateWithError(scene);
        var loaded = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(scene.Id);
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(update.Success, Is.False);
        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Lamps.Select(item => item.Id),
            Is.EqualTo(new[] { lamp!.Id }));
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("1"));
    }

    [Test]
    public async Task Deleting_many_to_many_linked_item_cleans_links_but_keeps_owner()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Linked deletion room",
            Code = "linked-deletion"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Linked deletion lamp",
            Room = room!
        });
        var scene = await TestScene.Create(new TestScene
        {
            Name = "Preserved owner",
            Lamps = [lamp!]
        });

        var deletion = await TestLamp.DeleteWithError(lamp!);
        var loadedOwner = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(scene!.Id);
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(loadedOwner.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loadedOwner.Errors));
        Assert.That(loadedOwner.Result, Is.Not.Null);
        Assert.That(loadedOwner.Result!.Lamps, Is.Empty);
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task Bulk_create_persists_many_to_many_links_for_each_owner()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Bulk relation room",
            Code = "bulk-relation"
        });
        var firstLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk relation one",
            Room = room!
        });
        var secondLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk relation two",
            Room = room!
        });
        var firstScene = new TestScene
        {
            Name = "Bulk scene one",
            Lamps = [firstLamp!]
        };
        var secondScene = new TestScene
        {
            Name = "Bulk scene two",
            Lamps = [firstLamp!, secondLamp!]
        };

        var creation = await TestScene.CreateWithError([firstScene, secondScene]);
        var firstLoaded = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(firstScene.Id);
        var secondLoaded = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(secondScene.Id);
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(firstScene.Id, Is.GreaterThan(0));
        Assert.That(secondScene.Id, Is.GreaterThan(0));
        Assert.That(firstLoaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(firstLoaded.Errors));
        Assert.That(firstLoaded.Result!.Lamps.Select(item => item.Id),
            Is.EqualTo(new[] { firstLamp!.Id }));
        Assert.That(secondLoaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondLoaded.Errors));
        Assert.That(secondLoaded.Result!.Lamps.Select(item => item.Id),
            Is.EquivalentTo(new[] { firstLamp.Id, secondLamp!.Id }));
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("3"));
    }

    [Test]
    public async Task Invalid_many_to_many_link_rolls_back_bulk_creation()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Bulk rollback room",
            Code = "bulk-rollback"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk rollback lamp",
            Room = room!
        });
        var validScene = new TestScene
        {
            Name = "Valid bulk relation",
            Lamps = [lamp!]
        };
        var invalidScene = new TestScene
        {
            Name = "Invalid bulk relation",
            Lamps = [TestLamp.OnlyId(999_999)]
        };

        var creation = await TestScene.CreateWithError([validScene, invalidScene]);
        var owners = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes\";");
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(creation.Success, Is.False);
        Assert.That(owners.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(owners.Errors));
        Assert.That(owners.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task Bulk_update_replaces_many_to_many_links_for_each_owner()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Bulk update room",
            Code = "bulk-update"
        });
        var firstLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk update one",
            Room = room!
        });
        var secondLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk update two",
            Room = room!
        });
        var firstScene = await TestScene.Create(new TestScene
        {
            Name = "Bulk updated one",
            Lamps = [firstLamp!]
        });
        var secondScene = await TestScene.Create(new TestScene
        {
            Name = "Bulk updated two",
            Lamps = [secondLamp!]
        });

        firstScene!.Lamps = [secondLamp!];
        secondScene!.Lamps = [];
        var update = await TestScene.UpdateWithError([firstScene, secondScene]);
        var firstLoaded = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(firstScene.Id);
        var secondLoaded = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(secondScene.Id);

        Assert.That(update.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(firstLoaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(firstLoaded.Errors));
        Assert.That(firstLoaded.Result!.Lamps.Select(item => item.Id),
            Is.EqualTo(new[] { secondLamp!.Id }));
        Assert.That(secondLoaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondLoaded.Errors));
        Assert.That(secondLoaded.Result!.Lamps, Is.Empty);
    }

    [Test]
    public async Task Invalid_many_to_many_link_rolls_back_all_bulk_updates()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Bulk update rollback room",
            Code = "bulk-update-rollback"
        });
        var firstLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk update preserved one",
            Room = room!
        });
        var secondLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk update preserved two",
            Room = room!
        });
        var firstScene = await TestScene.Create(new TestScene
        {
            Name = "Bulk rollback one",
            Lamps = [firstLamp!]
        });
        var secondScene = await TestScene.Create(new TestScene
        {
            Name = "Bulk rollback two",
            Lamps = [secondLamp!]
        });

        firstScene!.Lamps = [secondLamp!];
        secondScene!.Lamps = [TestLamp.OnlyId(999_999)];
        var update = await TestScene.UpdateWithError([firstScene, secondScene]);
        var firstLoaded = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(firstScene.Id);
        var secondLoaded = await ((TestSceneManager)GenericDM.Get<TestScene>())
            .GetByIdWithErrorNoCache<TestScene>(secondScene.Id);

        Assert.That(update.Success, Is.False);
        Assert.That(firstLoaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(firstLoaded.Errors));
        Assert.That(firstLoaded.Result!.Lamps.Select(item => item.Id),
            Is.EqualTo(new[] { firstLamp!.Id }));
        Assert.That(secondLoaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondLoaded.Errors));
        Assert.That(secondLoaded.Result!.Lamps.Select(item => item.Id),
            Is.EqualTo(new[] { secondLamp!.Id }));
        Assert.That(firstScene.Lamps, Has.Count.EqualTo(1));
        Assert.That(firstScene.Lamps[0], Is.SameAs(firstLamp));
        Assert.That(secondScene.Lamps, Has.Count.EqualTo(1));
        Assert.That(secondScene.Lamps[0], Is.SameAs(secondLamp));
    }

    [Test]
    public async Task Delete_on_cascade_removes_dependent_rows()
    {
        var room = await TestRoom.Create(new TestRoom { Name = "Garage" });
        var lamp = await TestLamp.Create(new TestLamp { Name = "Door", Room = room! });

        Assert.That(await room!.Delete(), Is.True);

        var rows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"test_lamps\" WHERE \"Id\" = {lamp!.Id};");
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task Reverse_link_loads_all_dependent_rows()
    {
        var room = await TestRoom.Create(new TestRoom { Name = "Hall", Code = "hall" });
        var first = await TestLamp.Create(new TestLamp { Name = "Entry", Room = room! });
        var second = await TestLamp.Create(new TestLamp { Name = "Stairs", Room = room! });

        var loaded = await ((TestRoomManager)AventusSharp.Data.Manager.GenericDM.Get<TestRoom>())
            .GetByIdWithErrorNoCache<TestRoom>(room!.Id);

        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Lamps.Select(item => item.Id),
            Is.EquivalentTo(new[] { first!.Id, second!.Id }));
    }

    [Test]
    public async Task Bidirectional_auto_read_uses_the_parent_instance_already_being_materialized()
    {
        var insertRoom = await IntegrationEnvironment.Storage.Execute(
            "INSERT INTO \"test_rooms\" (\"Name\", \"Code\", \"Description\") " +
            "VALUES ('Cycle room', 'cycle-room', NULL);");
        var roomRow = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Id\" FROM \"test_rooms\" WHERE \"Code\" = 'cycle-room';");
        Assert.That(insertRoom.Success, Is.True, IntegrationEnvironment.ErrorMessages(insertRoom.Errors));
        Assert.That(roomRow.Success, Is.True, IntegrationEnvironment.ErrorMessages(roomRow.Errors));
        var roomId = int.Parse(roomRow.Result!.Single()["Id"]!);

        var insertLamp = await IntegrationEnvironment.Storage.Execute(
            "INSERT INTO \"test_lamps\" (\"Name\", \"Room\") " +
            $"VALUES ('Cycle lamp', {roomId});");
        var lampRow = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Id\" FROM \"test_lamps\" WHERE \"Name\" = 'Cycle lamp';");
        Assert.That(insertLamp.Success, Is.True, IntegrationEnvironment.ErrorMessages(insertLamp.Errors));
        var lampId = int.Parse(lampRow.Result!.Single()["Id"]!);

        ((IDatabaseDM)GenericDM.Get<TestRoom>())
            .RemoveRecordsItems<TestRoom>([roomId]);
        ((IDatabaseDM)GenericDM.Get<TestLamp>())
            .RemoveRecordsItems<TestLamp>([lampId]);

        var loaded = await TestRoom.GetByIdWithError(roomId);

        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Lamps, Has.Count.EqualTo(1));
        Assert.That(loaded.Result.Lamps[0].Room, Is.SameAs(loaded.Result),
            "The child must reuse the parent currently being materialized instead of recursively loading it.");
    }

    [Test]
    public async Task Explicit_load_populates_a_lazy_relation_on_the_existing_instance()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Lazy room",
            Code = "lazy-room"
        });
        var link = await TestLazyLink.Create(new TestLazyLink
        {
            Name = "Lazy link",
            Room = room!
        });
        Assert.That(link, Is.Not.Null);

        ((IDatabaseDM)GenericDM.Get<TestLazyLink>())
            .RemoveRecordsItems<TestLazyLink>([link!.Id]);
        var loaded = await ((TestLazyLinkManager)GenericDM.Get<TestLazyLink>())
            .GetByIdWithErrorNoCache<TestLazyLink>(link.Id);

        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Room, Is.Null,
            "A relation without AutoRead must not be loaded by the initial query.");

        var sameInstance = loaded.Result;
        var load = await loaded.Result.Load(item => item.Room);

        Assert.Multiple(() =>
        {
            Assert.That(load.Success, Is.True, IntegrationEnvironment.ErrorMessages(load.Errors));
            Assert.That(loaded.Result, Is.SameAs(sameInstance));
            Assert.That(loaded.Result.Room, Is.SameAs(room));
        });
    }

    [Test]
    public async Task Query_Include_loads_a_relation_that_is_lazy_by_default()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Included room",
            Code = "included-room"
        });
        var link = await TestLazyLink.Create(new TestLazyLink
        {
            Name = "Included link",
            Room = room!
        });

        var withoutInclude = await ((TestLazyLinkManager)GenericDM.Get<TestLazyLink>())
            .GetByIdWithErrorNoCache<TestLazyLink>(link!.Id);
        var withInclude = await TestLazyLink.StartQuery()
            .Include(item => item.Room)
            .Where(item => item.Id == link.Id)
            .SingleWithError();

        Assert.That(withoutInclude.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(withoutInclude.Errors));
        Assert.That(withoutInclude.Result!.Room, Is.Null);
        Assert.That(withInclude.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(withInclude.Errors));
        Assert.That(withInclude.Result!.Room, Is.SameAs(room));
    }

    [Test]
    public async Task Query_Include_limits_joined_fields_but_cache_resolution_returns_the_complete_relation()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Projected relation",
            Code = "must-not-be-loaded",
            Description = "must-not-be-loaded"
        });
        var link = await TestLazyLink.Create(new TestLazyLink
        {
            Name = "Projected link",
            Room = room!
        });
        ((IDatabaseDM)GenericDM.Get<TestRoom>())
            .RemoveRecordsItems<TestRoom>([room!.Id]);

        var builder = TestLazyLink.StartQuery()
            .Include(item => item.Room, [item => item.Name])
            .Where(item => item.Id == link!.Id);
        var result = await builder.SingleWithError();
        var sql = ((AventusSharp.Data.Manager.DB.Builders.DatabaseQueryBuilder<TestLazyLink>)builder)
            .info!.Sql;

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(sql, Does.Not.Contain("*Code"), sql);
        Assert.That(sql, Does.Not.Contain("*Description"), sql);
        Assert.Multiple(() =>
        {
            Assert.That(result.Result!.Room, Is.Not.Null);
            Assert.That(result.Result.Room.Name, Is.EqualTo("Projected relation"));
            Assert.That(result.Result.Room.Code, Is.EqualTo("must-not-be-loaded"));
            Assert.That(result.Result.Room.Description, Is.EqualTo("must-not-be-loaded"));
        });
    }

    [Test]
    [Explicit("Specification: nested explicit loading does not yet populate a reverse link on the related object.")]
    public async Task Explicit_load_supports_a_nested_reverse_link_path()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Nested room",
            Code = "nested-room"
        });
        var first = await TestLamp.Create(new TestLamp
        {
            Name = "Nested first",
            Room = room!
        });
        var second = await TestLamp.Create(new TestLamp
        {
            Name = "Nested second",
            Room = room!
        });
        var link = await TestLazyLink.Create(new TestLazyLink
        {
            Name = "Nested link",
            Room = room!
        });
        ((IDatabaseDM)GenericDM.Get<TestLazyLink>())
            .RemoveRecordsItems<TestLazyLink>([link!.Id]);

        var loaded = await ((TestLazyLinkManager)GenericDM.Get<TestLazyLink>())
            .GetByIdWithErrorNoCache<TestLazyLink>(link.Id);
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Room, Is.Null);

        var load = await loaded.Result.Load(item => item.Room.Lamps);

        Assert.That(load.Success, Is.True, IntegrationEnvironment.ErrorMessages(load.Errors));
        Assert.That(loaded.Result.Room, Is.SameAs(room));
        Assert.That(loaded.Result.Room.Lamps.Select(item => item.Id),
            Is.EquivalentTo(new[] { first!.Id, second!.Id }));
        Assert.That(loaded.Result.Room.Lamps,
            Has.All.Matches<TestLamp>(lamp => ReferenceEquals(lamp.Room, room)));
    }

    [Test]
    public async Task DeleteSetNull_keeps_the_dependent_row_and_clears_its_relation()
    {
        var room = await TestRoom.Create(new TestRoom { Name = "Utility", Code = "utility" });
        var sensor = await TestSensor.Create(new TestSensor { Name = "Humidity", Room = room });

        var deletion = await TestRoom.DeleteWithError(room!);
        Assert.That(deletion.Success, Is.True, IntegrationEnvironment.ErrorMessages(deletion.Errors));

        var loaded = await ((TestSensorManager)AventusSharp.Data.Manager.GenericDM.Get<TestSensor>())
            .GetByIdWithErrorNoCache<TestSensor>(sensor!.Id);
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result, Is.Not.Null);
        Assert.That(loaded.Result!.Room, Is.Null);
    }

    [Test]
    public async Task Bulk_parent_delete_applies_cascade_and_set_null_to_every_relation()
    {
        var firstRoom = await TestRoom.Create(new TestRoom
        {
            Name = "Bulk delete room one",
            Code = "bulk-delete-one"
        });
        var secondRoom = await TestRoom.Create(new TestRoom
        {
            Name = "Bulk delete room two",
            Code = "bulk-delete-two"
        });
        var firstLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk deleted lamp one",
            Room = firstRoom!
        });
        var secondLamp = await TestLamp.Create(new TestLamp
        {
            Name = "Bulk deleted lamp two",
            Room = secondRoom!
        });
        var firstSensor = await TestSensor.Create(new TestSensor
        {
            Name = "Bulk preserved sensor one",
            Room = firstRoom
        });
        var secondSensor = await TestSensor.Create(new TestSensor
        {
            Name = "Bulk preserved sensor two",
            Room = secondRoom
        });

        var deletion = await TestRoom.DeleteWithError([firstRoom!, secondRoom!]);
        var lampRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_lamps\";");
        var firstLoadedSensor = await ((TestSensorManager)GenericDM.Get<TestSensor>())
            .GetByIdWithErrorNoCache<TestSensor>(firstSensor!.Id);
        var secondLoadedSensor = await ((TestSensorManager)GenericDM.Get<TestSensor>())
            .GetByIdWithErrorNoCache<TestSensor>(secondSensor!.Id);

        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(lampRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(lampRows.Errors));
        Assert.That(lampRows.Result!.Single()["count"], Is.EqualTo("0"),
            $"Lamps {firstLamp!.Id} and {secondLamp!.Id} must be deleted by cascade.");
        Assert.That(firstLoadedSensor.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(firstLoadedSensor.Errors));
        Assert.That(firstLoadedSensor.Result!.Room, Is.Null);
        Assert.That(secondLoadedSensor.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondLoadedSensor.Errors));
        Assert.That(secondLoadedSensor.Result!.Room, Is.Null);
    }

    [Test]
    [Explicit("Specification: database-side DeleteSetNull does not yet synchronize an already cached dependent instance.")]
    public async Task DeleteSetNull_updates_the_dependent_instance_already_in_cache()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Cached set-null room",
            Code = "cached-set-null"
        });
        var sensor = await TestSensor.Create(new TestSensor
        {
            Name = "Cached set-null sensor",
            Room = room
        });
        var cachedSensor = await TestSensor.GetByIdWithError(sensor!.Id);

        var deletion = await TestRoom.DeleteWithError(room!);
        var afterDeletion = await TestSensor.GetByIdWithError(sensor.Id);

        Assert.That(cachedSensor.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(cachedSensor.Errors));
        Assert.That(cachedSensor.Result, Is.SameAs(sensor));
        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(sensor.Room, Is.Null);
        Assert.That(afterDeletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(afterDeletion.Errors));
        Assert.That(afterDeletion.Result, Is.SameAs(sensor));
        Assert.That(afterDeletion.Result!.Room, Is.Null);
    }

    [Test]
    public async Task DeleteOnCascade_removes_the_dependent_instance_from_cache()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Cached cascade room",
            Code = "cached-cascade"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Cached cascade lamp",
            Room = room!
        });
        var cachedLamp = await TestLamp.GetByIdWithError(lamp!.Id);

        var deletion = await TestRoom.DeleteWithError(room!);
        var afterDeletion = await TestLamp.GetByIdWithError(lamp.Id);

        Assert.That(cachedLamp.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(cachedLamp.Errors));
        Assert.That(cachedLamp.Result, Is.SameAs(lamp));
        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(afterDeletion.Success, Is.False);
        Assert.That(afterDeletion.Result, Is.Null);
    }

    [Test]
    public async Task Parent_delete_rollback_restores_cascaded_rows_and_cached_relations()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Delete rollback room",
            Code = "delete-rollback-room"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Delete rollback lamp",
            Room = room!
        });
        var sensor = await TestSensor.Create(new TestSensor
        {
            Name = "Delete rollback sensor",
            Room = room
        });
        var cachedRoom = await TestRoom.GetByIdWithError(room!.Id);
        var cachedLamp = await TestLamp.GetByIdWithError(lamp!.Id);
        var cachedSensor = await TestSensor.GetByIdWithError(sensor!.Id);
        var roomManager = GenericDM.Get<TestRoom>();

        var transaction = await roomManager.RunInsideTransaction(async () =>
        {
            var deletion = await TestRoom.DeleteWithError(room);
            deletion.Errors.Add(new AventusSharp.Tools.GenericError(
                9930,
                "force parent deletion rollback"));
            return deletion;
        });
        var roomAfterRollback = await TestRoom.GetByIdWithError(room.Id);
        var lampAfterRollback = await TestLamp.GetByIdWithError(lamp.Id);
        var sensorAfterRollback = await TestSensor.GetByIdWithError(sensor.Id);

        Assert.That(cachedRoom.Result, Is.SameAs(room));
        Assert.That(cachedLamp.Result, Is.SameAs(lamp));
        Assert.That(cachedSensor.Result, Is.SameAs(sensor));
        Assert.That(transaction.Success, Is.False);
        Assert.That(roomAfterRollback.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(roomAfterRollback.Errors));
        Assert.That(roomAfterRollback.Result, Is.SameAs(room));
        Assert.That(lampAfterRollback.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(lampAfterRollback.Errors));
        Assert.That(lampAfterRollback.Result, Is.SameAs(lamp));
        Assert.That(lamp.Room, Is.SameAs(room));
        Assert.That(sensorAfterRollback.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(sensorAfterRollback.Errors));
        Assert.That(sensorAfterRollback.Result, Is.SameAs(sensor));
        Assert.That(sensor.Room, Is.SameAs(room));
    }

    [Test]
    public async Task AutoCRUD_manages_the_lifecycle_of_an_owned_relation()
    {
        var owner = new TestOwnedEntity
        {
            Name = "Controller",
            Profile = new TestOwnedProfile { Label = "Initial" }
        };

        var creation = await TestOwnedEntity.CreateWithError(owner);
        Assert.That(creation.Success, Is.True, IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(owner.Profile.Id, Is.GreaterThan(0));

        owner.Profile.Label = "Updated";
        var update = await TestOwnedEntity.UpdateWithError(owner);
        var storedProfile = await ((TestOwnedProfileManager)AventusSharp.Data.Manager.GenericDM.Get<TestOwnedProfile>())
            .GetByIdWithErrorNoCache<TestOwnedProfile>(owner.Profile.Id);
        Assert.That(update.Success, Is.True, IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(storedProfile.Result!.Label, Is.EqualTo("Updated"));

        var profileId = owner.Profile.Id;
        var deletion = await TestOwnedEntity.DeleteWithError(owner);
        var deletedProfile = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"test_owned_profiles\" WHERE \"Id\" = {profileId};");
        Assert.That(deletion.Success, Is.True, IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(deletedProfile.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task AutoCRUD_replaces_an_owned_relation_and_deletes_the_previous_item()
    {
        var owner = await TestOwnedEntity.Create(new TestOwnedEntity
        {
            Name = "Replace controller",
            Profile = new TestOwnedProfile { Label = "Previous profile" }
        });
        var previousProfileId = owner!.Profile.Id;
        var replacement = new TestOwnedProfile { Label = "Replacement profile" };

        owner.Profile = replacement;
        var update = await TestOwnedEntity.UpdateWithError(owner);
        var loadedOwner = await ((TestOwnedEntityManager)GenericDM.Get<TestOwnedEntity>())
            .GetByIdWithErrorNoCache<TestOwnedEntity>(owner.Id);
        var previousRows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"test_owned_profiles\" WHERE \"Id\" = {previousProfileId};");

        Assert.That(update.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(replacement.Id, Is.GreaterThan(0));
        Assert.That(replacement.Id, Is.Not.EqualTo(previousProfileId));
        Assert.That(loadedOwner.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loadedOwner.Errors));
        Assert.That(loadedOwner.Result!.Profile.Id, Is.EqualTo(replacement.Id));
        Assert.That(loadedOwner.Result.Profile.Label, Is.EqualTo("Replacement profile"));
        Assert.That(previousRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(previousRows.Errors));
        Assert.That(previousRows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task AutoCRUD_manages_owned_relations_during_bulk_create_and_update()
    {
        var firstOwner = new TestOwnedEntity
        {
            Name = "Bulk owner one",
            Profile = new TestOwnedProfile { Label = "Bulk profile one" }
        };
        var secondOwner = new TestOwnedEntity
        {
            Name = "Bulk owner two",
            Profile = new TestOwnedProfile { Label = "Bulk profile two" }
        };

        var creation = await TestOwnedEntity.CreateWithError([firstOwner, secondOwner]);
        var firstProfileId = firstOwner.Profile.Id;
        var previousSecondProfileId = secondOwner.Profile.Id;

        firstOwner.Profile.Label = "Bulk profile one updated";
        secondOwner.Profile = new TestOwnedProfile
        {
            Label = "Bulk profile two replaced"
        };
        var update = await TestOwnedEntity.UpdateWithError([firstOwner, secondOwner]);
        var firstLoaded = await ((TestOwnedEntityManager)GenericDM.Get<TestOwnedEntity>())
            .GetByIdWithErrorNoCache<TestOwnedEntity>(firstOwner.Id);
        var secondLoaded = await ((TestOwnedEntityManager)GenericDM.Get<TestOwnedEntity>())
            .GetByIdWithErrorNoCache<TestOwnedEntity>(secondOwner.Id);
        var previousSecondRows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"test_owned_profiles\" WHERE \"Id\" = {previousSecondProfileId};");

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(firstOwner.Id, Is.GreaterThan(0));
        Assert.That(secondOwner.Id, Is.GreaterThan(0));
        Assert.That(firstProfileId, Is.GreaterThan(0));
        Assert.That(previousSecondProfileId, Is.GreaterThan(0));
        Assert.That(update.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(firstLoaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(firstLoaded.Errors));
        Assert.That(firstLoaded.Result!.Profile.Id, Is.EqualTo(firstProfileId));
        Assert.That(firstLoaded.Result.Profile.Label, Is.EqualTo("Bulk profile one updated"));
        Assert.That(secondLoaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondLoaded.Errors));
        Assert.That(secondLoaded.Result!.Profile.Id, Is.EqualTo(secondOwner.Profile.Id));
        Assert.That(secondLoaded.Result.Profile.Label, Is.EqualTo("Bulk profile two replaced"));
        Assert.That(previousSecondRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(previousSecondRows.Errors));
        Assert.That(previousSecondRows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task AutoCRUD_failure_rolls_back_bulk_owners_and_owned_items()
    {
        var firstOwner = new TestOwnedEntity
        {
            Name = "Rollback owner one",
            Profile = new TestOwnedProfile { Label = "Duplicate owned profile" }
        };
        var secondOwner = new TestOwnedEntity
        {
            Name = "Rollback owner two",
            Profile = new TestOwnedProfile { Label = "Duplicate owned profile" }
        };

        var creation = await TestOwnedEntity.CreateWithError([firstOwner, secondOwner]);
        var ownerRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_owners\";");
        var profileRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_owned_profiles\";");
        var firstCachedOwner = await TestOwnedEntity.GetByIdWithError(firstOwner.Id);
        var firstCachedProfile = await TestOwnedProfile.GetByIdWithError(firstOwner.Profile.Id);

        Assert.That(creation.Success, Is.False);
        Assert.That(ownerRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(ownerRows.Errors));
        Assert.That(ownerRows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(profileRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(profileRows.Errors));
        Assert.That(profileRows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(firstCachedOwner.Success, Is.False);
        Assert.That(firstCachedOwner.Result, Is.Null);
        Assert.That(firstCachedProfile.Success, Is.False);
        Assert.That(firstCachedProfile.Result, Is.Null);
    }
}
