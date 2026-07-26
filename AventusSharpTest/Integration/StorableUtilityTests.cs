using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class StorableUtilityTests
{
    [SetUp]
    public async Task ClearData()
    {
        var roomIds = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Id\" FROM \"test_rooms\";");
        Assert.That(roomIds.Success, Is.True, IntegrationEnvironment.ErrorMessages(roomIds.Errors));
        ((IDatabaseDM)GenericDM.Get<TestRoom>()).RemoveRecordsItems<TestRoom>(
            roomIds.Result!.Select(row => int.Parse(row["Id"]!)).ToList());

        var lampIds = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Id\" FROM \"test_lamps\";");
        Assert.That(lampIds.Success, Is.True, IntegrationEnvironment.ErrorMessages(lampIds.Errors));
        ((IDatabaseDM)GenericDM.Get<TestLamp>()).RemoveRecordsItems<TestLamp>(
            lampIds.Result!.Select(row => int.Parse(row["Id"]!)).ToList());

        var result = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_scenes_test_lamps\";" +
            "DELETE FROM \"test_scenes\";" +
            "DELETE FROM \"test_sensors\";" +
            "DELETE FROM \"test_lamps\";" +
            "DELETE FROM \"test_rooms\";");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public void OnlyId_creates_a_reference_without_calling_the_constructor()
    {
        ConstructorTrackedRecord.ConstructorCalls = 0;

        var reference = ConstructorTrackedRecord.OnlyId(73);

        Assert.That(reference.Id, Is.EqualTo(73));
        Assert.That(ConstructorTrackedRecord.ConstructorCalls, Is.Zero);
    }

    [Test]
    public void EnableDebug_and_DisableDebug_toggle_the_registered_storage()
    {
        TestRoom.DisableDebug();
        Assert.That(IntegrationEnvironment.Storage.Debug, Is.False);

        TestRoom.EnableDebug();
        Assert.That(IntegrationEnvironment.Storage.Debug, Is.True);

        TestRoom.DisableDebug();
        Assert.That(IntegrationEnvironment.Storage.Debug, Is.False);
    }

    [Test]
    public async Task LoadObjectFromId_assigns_the_same_cached_instance()
    {
        var room = await TestRoom.Create(new TestRoom { Name = "Office", Code = "load-one" });
        var holder = new ExplicitLinkHolder { RoomId = room!.Id };

        var result = await holder.LoadObjectFromId<TestRoom>(
            item => item.RoomId,
            (item, loadedRoom) => item.Room = loadedRoom);

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.SameAs(room));
        Assert.That(holder.Room, Is.SameAs(room));
    }

    [Test]
    public async Task LoadObjectsFromIds_deduplicates_the_query_but_preserves_assignments()
    {
        var first = await TestRoom.Create(new TestRoom { Name = "First", Code = "load-list-1" });
        var second = await TestRoom.Create(new TestRoom { Name = "Second", Code = "load-list-2" });
        var left = new ExplicitLinkHolder { RoomIds = [first!.Id, second!.Id, first.Id] };
        var right = new ExplicitLinkHolder { RoomIds = [second.Id] };

        var result = await ListStorable.LoadObjectsFromIds<ExplicitLinkHolder, TestRoom>(
            new List<ExplicitLinkHolder> { left, right },
            item => item.RoomIds,
            (item, room) => item.Rooms.Add(room));

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(2));
        Assert.That(left.Rooms, Is.EqualTo(new[] { first, second, first }));
        Assert.That(right.Rooms, Is.EqualTo(new[] { second }));
        Assert.That(left.Rooms[0], Is.SameAs(left.Rooms[2]));
        Assert.That(right.Rooms[0], Is.SameAs(second));
    }

    [Test]
    public async Task Loader_propagates_an_upstream_result_error_without_querying()
    {
        var upstream = new AventusSharp.Tools.ResultWithError<List<ExplicitLinkHolder>>();
        upstream.Errors.Add(new DataError(DataErrorCode.ValidationError, "Upstream failure"));

        var result = await ExplicitLinkHolder.LoadObjectFromId<TestRoom>(
            upstream,
            item => item.RoomId,
            (item, room) => item.Room = room);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.OfType<DataError>().Select(error => error.Code),
            Does.Contain(DataErrorCode.ValidationError));
        Assert.That(result.Result, Is.Null);
    }
}

[ManualInit]
public sealed class ExplicitLinkHolder : Storable<ExplicitLinkHolder>
{
    public int RoomId { get; set; }
    public List<int> RoomIds { get; set; } = [];
    public TestRoom? Room { get; set; }
    public List<TestRoom> Rooms { get; set; } = [];
}

[ManualInit]
public sealed class ConstructorTrackedRecord : Storable<ConstructorTrackedRecord>
{
    public static int ConstructorCalls { get; set; }

    public ConstructorTrackedRecord()
    {
        ConstructorCalls++;
    }
}
