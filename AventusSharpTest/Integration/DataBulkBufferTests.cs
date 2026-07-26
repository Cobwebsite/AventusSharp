using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataBulkBufferTests
{
    [SetUp]
    public async Task ClearTables()
    {
        var result = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_scenes_test_lamps\";" +
            "DELETE FROM \"test_scenes\";" +
            "DELETE FROM \"test_lamps\";" +
            "DELETE FROM \"test_rooms\";" +
            "DELETE FROM \"test_owners\";" +
            "DELETE FROM \"test_owned_profiles\";");
        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [TestCase(499)]
    [TestCase(500)]
    [TestCase(501)]
    public async Task BulkCreate_persists_rows_across_buffer_boundaries(int count)
    {
        var profiles = Enumerable.Range(0, count)
            .Select(index => new TestOwnedProfile
            {
                Label = $"Buffered profile {count}-{index}"
            })
            .ToList();

        var creation = await TestOwnedProfile.BulkCreateWithError(profiles);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_owned_profiles\";");

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(rows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo(count.ToString()));
    }

    [Test]
    public async Task Failure_in_second_bulk_buffer_rolls_back_the_first_buffer()
    {
        var profiles = Enumerable.Range(0, 501)
            .Select(index => new TestOwnedProfile
            {
                Label = $"Rollback buffered profile {index}"
            })
            .ToList();
        profiles[500].Label = profiles[0].Label;

        var creation = await TestOwnedProfile.BulkCreateWithError(profiles);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_owned_profiles\";");
        var cached = await TestOwnedProfile.GetAllWithError();

        Assert.That(creation.Success, Is.False);
        Assert.That(rows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(cached.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(cached.Errors));
        Assert.That(cached.Result, Is.Empty);
    }

    [Test]
    public async Task BulkCreate_withId_preserves_ids_across_multiple_buffers()
    {
        var profiles = Enumerable.Range(0, 501)
            .Select(index => new TestOwnedProfile
            {
                Id = 10_000 + index,
                Label = $"Explicit id profile {index}"
            })
            .ToList();

        var creation = await TestOwnedProfile.BulkCreateWithError(profiles, withId: true);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT MIN(\"Id\") AS min_id, MAX(\"Id\") AS max_id, " +
            "COUNT(*) AS count FROM \"test_owned_profiles\";");

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(rows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(rows.Errors));
        var summary = rows.Result!.Single();
        Assert.That(summary["min_id"], Is.EqualTo("10000"));
        Assert.That(summary["max_id"], Is.EqualTo("10500"));
        Assert.That(summary["count"], Is.EqualTo("501"));
    }

    [Test]
    [Explicit("Specification: optimized BulkCreate does not yet persist many-to-many intermediate rows.")]
    public async Task BulkCreate_withId_persists_many_to_many_links_across_buffers()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Buffered relation room",
            Code = "buffered-relation"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Buffered relation lamp",
            Room = room!
        });
        var scenes = Enumerable.Range(0, 501)
            .Select(index => new TestScene
            {
                Id = 20_000 + index,
                Name = $"Buffered relation scene {index}",
                Lamps = [lamp!]
            })
            .ToList();

        var creation = await TestScene.BulkCreateWithError(scenes, withId: true);
        var ownerRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes\";");
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(ownerRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(ownerRows.Errors));
        Assert.That(ownerRows.Result!.Single()["count"], Is.EqualTo("501"));
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("501"));
    }

    [Test]
    [Explicit("Specification: optimized BulkCreate currently ignores many-to-many values, including invalid links.")]
    public async Task Invalid_many_to_many_link_in_second_buffer_rolls_back_all_buffers()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Buffered rollback room",
            Code = "buffered-rollback"
        });
        var lamp = await TestLamp.Create(new TestLamp
        {
            Name = "Buffered rollback lamp",
            Room = room!
        });
        var scenes = Enumerable.Range(0, 501)
            .Select(index => new TestScene
            {
                Id = 30_000 + index,
                Name = $"Buffered rollback scene {index}",
                Lamps = [lamp!]
            })
            .ToList();
        scenes[500].Lamps = [TestLamp.OnlyId(999_999)];

        var creation = await TestScene.BulkCreateWithError(scenes, withId: true);
        var ownerRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes\";");
        var intermediateRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_scenes_test_lamps\";");

        Assert.That(creation.Success, Is.False);
        Assert.That(ownerRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(ownerRows.Errors));
        Assert.That(ownerRows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(intermediateRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(intermediateRows.Errors));
        Assert.That(intermediateRows.Result!.Single()["count"], Is.EqualTo("0"));
    }

    [Test]
    public async Task BulkCreate_withId_persists_direct_relations_across_buffers()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Buffered direct room",
            Code = "buffered-direct"
        });
        var lamps = Enumerable.Range(0, 501)
            .Select(index => new TestLamp
            {
                Id = 40_000 + index,
                Name = $"Buffered direct lamp {index}",
                Room = room!
            })
            .ToList();

        var creation = await TestLamp.BulkCreateWithError(lamps, withId: true);
        var linkedRows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"test_lamps\" WHERE \"Room\" = {room!.Id};");
        var beforeBoundary = await TestLamp.GetByIdWithError(lamps[499].Id);
        var afterBoundary = await TestLamp.GetByIdWithError(lamps[500].Id);

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(linkedRows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(linkedRows.Errors));
        Assert.That(linkedRows.Result!.Single()["count"], Is.EqualTo("501"));
        Assert.That(beforeBoundary.Result, Is.SameAs(lamps[499]));
        Assert.That(afterBoundary.Result, Is.SameAs(lamps[500]));
        Assert.That(beforeBoundary.Result!.Room, Is.SameAs(room));
        Assert.That(afterBoundary.Result!.Room, Is.SameAs(room));
    }

    [Test]
    public async Task BulkCreate_withId_replaces_an_OnlyId_relation_with_the_canonical_parent()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Canonicalized bulk room",
            Code = "canonicalized-bulk-room"
        });
        Assert.That(room, Is.Not.Null);
        var lamp = new TestLamp
        {
            Id = 45_001,
            Name = "Bulk lamp with short relation",
            Room = TestRoom.OnlyId(room!.Id)
        };

        var creation = await TestLamp.BulkCreateWithError([lamp], withId: true);
        var cached = await TestLamp.GetByIdWithError(lamp.Id);
        var noCache = await ((TestLampManager)
                AventusSharp.Data.Manager.GenericDM.Get<TestLamp>())
            .GetByIdWithErrorNoCache<TestLamp>(lamp.Id);

        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(creation.Errors));
            Assert.That(cached.Result, Is.SameAs(lamp));
            Assert.That(lamp.Room, Is.SameAs(room));
            Assert.That(noCache.Result!.Room, Is.SameAs(room));
        });
    }

    [Test]
    public async Task BulkCreate_withId_relation_rollback_restores_the_original_OnlyId_reference()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Rolled back canonicalized room",
            Code = "rolled-back-canonicalized-room"
        });
        Assert.That(room, Is.Not.Null);
        var originalShortRelation = TestRoom.OnlyId(room!.Id);
        var lamp = new TestLamp
        {
            Id = 45_002,
            Name = "Rolled back lamp with short relation",
            Room = originalShortRelation
        };
        var manager = AventusSharp.Data.Manager.GenericDM.Get<TestLamp>();

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var creation = await TestLamp.BulkCreateWithError([lamp], withId: true);
            Assert.That(lamp.Room, Is.SameAs(room),
                "The relation must be canonical while the transaction is active.");
            creation.Errors.Add(new AventusSharp.Tools.GenericError(
                9925, "force canonicalized relation rollback"));
            return creation;
        });
        var cached = await TestLamp.GetByIdWithError(lamp.Id);
        var roomCached = await TestRoom.GetByIdWithError(room.Id);

        Assert.Multiple(() =>
        {
            Assert.That(transaction.Success, Is.False);
            Assert.That(cached.Success, Is.False);
            Assert.That(cached.Result, Is.Null);
            Assert.That(lamp.Room, Is.SameAs(originalShortRelation));
            Assert.That(roomCached.Result, Is.SameAs(room));
        });
    }

    [Test]
    public async Task Invalid_direct_link_in_second_buffer_rolls_back_all_buffers()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Buffered direct rollback room",
            Code = "buffered-direct-rollback"
        });
        var lamps = Enumerable.Range(0, 501)
            .Select(index => new TestLamp
            {
                Id = 50_000 + index,
                Name = $"Buffered direct rollback lamp {index}",
                Room = room!
            })
            .ToList();
        lamps[500].Room = TestRoom.OnlyId(999_999);

        var creation = await TestLamp.BulkCreateWithError(lamps, withId: true);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_lamps\";");
        var firstCached = await TestLamp.GetByIdWithError(lamps[0].Id);
        var failingCached = await TestLamp.GetByIdWithError(lamps[500].Id);

        Assert.That(creation.Success, Is.False);
        Assert.That(rows.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(firstCached.Success, Is.False);
        Assert.That(firstCached.Result, Is.Null);
        Assert.That(failingCached.Success, Is.False);
        Assert.That(failingCached.Result, Is.Null);
    }

    [Test]
    public async Task Outer_rollback_after_direct_relation_bulk_removes_children_but_keeps_parent()
    {
        var room = await TestRoom.Create(new TestRoom
        {
            Name = "Outer rollback direct room",
            Code = "outer-rollback-direct"
        });
        Assert.That(room, Is.Not.Null);
        var lamps = Enumerable.Range(0, 501)
            .Select(index => new TestLamp
            {
                Id = 60_000 + index,
                Name = $"Outer rollback direct lamp {index}",
                Room = room!
            })
            .ToList();
        var manager = AventusSharp.Data.Manager.GenericDM.Get<TestLamp>();

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var creation = await TestLamp.BulkCreateWithError(lamps, withId: true);
            creation.Errors.Add(new AventusSharp.Tools.GenericError(
                9924, "force direct relation bulk rollback"));
            return creation;
        });
        var lampRows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_lamps\";");
        var roomRows = await IntegrationEnvironment.Storage.Query(
            $"SELECT COUNT(*) AS count FROM \"test_rooms\" WHERE \"Id\" = {room!.Id};");
        var firstCached = await TestLamp.GetByIdWithError(lamps[0].Id);
        var lastCached = await TestLamp.GetByIdWithError(lamps[^1].Id);
        var roomCached = await TestRoom.GetByIdWithError(room.Id);

        Assert.Multiple(() =>
        {
            Assert.That(transaction.Success, Is.False);
            Assert.That(lampRows.Result!.Single()["count"], Is.EqualTo("0"));
            Assert.That(roomRows.Result!.Single()["count"], Is.EqualTo("1"));
            Assert.That(firstCached.Success, Is.False);
            Assert.That(lastCached.Success, Is.False);
            Assert.That(roomCached.Result, Is.SameAs(room));
        });
    }
}
