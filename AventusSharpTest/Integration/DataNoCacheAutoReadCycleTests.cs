using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataNoCacheAutoReadCycleTests
{
    [Test]
    public async Task Bidirectional_auto_read_reuses_instances_without_local_cache()
    {
        var room = await NoCacheCycleRoom.Create(new NoCacheCycleRoom { Name = "Cycle room" });
        Assert.That(room, Is.Not.Null);
        var lamp = await NoCacheCycleLamp.Create(new NoCacheCycleLamp
        {
            Name = "Cycle lamp",
            Room = room!
        });
        Assert.That(lamp, Is.Not.Null);

        var manager = (NoCacheCycleRoomManager)GenericDM.Get<NoCacheCycleRoom>();
        Assert.That(manager.NeedLocalCache, Is.False);
        var loaded = await manager.GetByIdWithErrorNoCache<NoCacheCycleRoom>(room!.Id);

        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Lamps, Has.Count.EqualTo(1));
        Assert.That(loaded.Result.Lamps[0].Room, Is.SameAs(loaded.Result));

        var secondRead = await manager.GetByIdWithErrorNoCache<NoCacheCycleRoom>(room.Id);
        Assert.That(secondRead.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(secondRead.Errors));
        Assert.That(secondRead.Result, Is.Not.SameAs(loaded.Result),
            "The materialization identity map must not become a local cache.");
        Assert.That(secondRead.Result!.Lamps[0].Room, Is.SameAs(secondRead.Result));
    }

    [Test]
    public async Task Multiple_roots_keep_their_own_bidirectional_auto_read_graphs()
    {
        var first = await NoCacheCycleRoom.Create(new NoCacheCycleRoom { Name = "First cycle room" });
        var second = await NoCacheCycleRoom.Create(new NoCacheCycleRoom { Name = "Second cycle room" });
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        await NoCacheCycleLamp.Create(new NoCacheCycleLamp { Name = "First lamp", Room = first! });
        await NoCacheCycleLamp.Create(new NoCacheCycleLamp { Name = "Second lamp", Room = second! });

        var loaded = await NoCacheCycleRoom.GetAllWithError();

        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        var roots = loaded.Result!.Where(item => item.Id == first!.Id || item.Id == second!.Id).ToList();
        Assert.That(roots, Has.Count.EqualTo(2));
        Assert.That(roots, Has.All.Matches<NoCacheCycleRoom>(room =>
            room.Lamps.Count == 1 && ReferenceEquals(room.Lamps[0].Room, room)));
    }
}
