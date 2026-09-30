using AventusSharp.Data.Manager;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Attributes;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataValueCollectionTests
{
    [Test]
    public void Foreign_key_integer_list_keeps_its_relation_mapping()
    {
        var property = typeof(ForeignKeyListProbe).GetProperty(nameof(ForeignKeyListProbe.RoomIds))!;
        var member = TableMemberInfoSql.CreateSql(property, new TableInfo(typeof(ForeignKeyListProbe)));
        Assert.That(member, Is.TypeOf<TableMemberInfoSqlNMInt>());

        var lamps = typeof(ForeignKeyListProbe).GetProperty(nameof(ForeignKeyListProbe.Lamps))!;
        var relation = TableMemberInfoSql.CreateSql(lamps, new TableInfo(typeof(ForeignKeyListProbe)));
        Assert.That(relation, Is.TypeOf<TableMemberInfoSqlNM>());
    }

    [SetUp]
    public async Task ClearTable()
    {
        var clear = await TestValueCollections.StartDelete()
            .Where(item => item.Id > 0).RunWithError();
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));
    }

    [Test]
    public async Task Lists_and_dictionaries_round_trip_with_enums_and_null_values()
    {
        var item = new TestValueCollections
        {
            Numbers = [1, -2, 1],
            Labels = ["a,b", null, "Été", ""],
            States = [PrimitiveRecordState.Ready, PrimitiveRecordState.Disabled],
            OptionalStates = [null, PrimitiveRecordState.Ready],
            OptionalNumbers = [null, 0, -3],
            Counts = new() { ["x"] = 2, ["none"] = null },
            StateByKey = new() { [1] = PrimitiveRecordState.Disabled, [2] = null },
            NullableLabels = null
        };

        var created = await TestValueCollections.CreateWithError(item);
        var loaded = await ((TestValueCollectionsManager)GenericDM.Get<TestValueCollections>())
            .GetByIdWithErrorNoCache<TestValueCollections>(item.Id);

        Assert.That(created.Success && loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(created.Errors.Concat(loaded.Errors)));
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Result!.Numbers, Is.EqualTo(new[] { 1, -2, 1 }));
            Assert.That(loaded.Result.Labels, Is.EqualTo(new string?[] { "a,b", null, "Été", "" }));
            Assert.That(loaded.Result.States, Is.EqualTo(new[] { PrimitiveRecordState.Ready, PrimitiveRecordState.Disabled }));
            Assert.That(loaded.Result.OptionalStates, Is.EqualTo(new PrimitiveRecordState?[] { null, PrimitiveRecordState.Ready }));
            Assert.That(loaded.Result.OptionalNumbers, Is.EqualTo(new int?[] { null, 0, -3 }));
            Assert.That(loaded.Result.Counts, Is.EquivalentTo(item.Counts));
            Assert.That(loaded.Result.StateByKey, Is.EquivalentTo(item.StateByKey));
            Assert.That(loaded.Result.NullableLabels, Is.Null);
        });
    }

    [Test]
    public async Task Collections_can_be_replaced_queried_and_deleted()
    {
        var item = (await TestValueCollections.Create(new TestValueCollections
        {
            Numbers = [1], States = [PrimitiveRecordState.Unknown]
        }))!;
        item.Numbers = [2, 3];
        item.States = [PrimitiveRecordState.Ready];
        item.Counts = new() { ["ready"] = 5 };

        var updated = await TestValueCollections.UpdateWithError(item);
        var queried = await TestValueCollections.StartQuery()
            .Where(value => value.Numbers.Contains(2) &&
                            value.States.Contains(PrimitiveRecordState.Ready) &&
                            value.Counts.ContainsKey("ready"))
            .RunWithError();
        var deleted = await TestValueCollections.DeleteWithError(item);
        var afterDelete = await TestValueCollections.StartQuery()
            .Where(value => value.Id == item.Id).RunWithError();

        Assert.That(updated.Success && queried.Success && deleted.Success && afterDelete.Success,
            Is.True, IntegrationEnvironment.ErrorMessages(updated.Errors.Concat(queried.Errors)
                .Concat(deleted.Errors).Concat(afterDelete.Errors)));
        Assert.That(queried.Result, Has.Count.EqualTo(1));
        Assert.That(queried.Result![0].Id, Is.EqualTo(item.Id));
        Assert.That(queried.Result![0].Numbers, Is.EqualTo(new[] { 2, 3 }));
        Assert.That(afterDelete.Result, Is.Empty);
    }
}

[ManualInit]
public sealed class ForeignKeyListProbe : AventusSharp.Data.Storable<ForeignKeyListProbe>
{
    [ForeignKey<TestRoom>]
    public List<int> RoomIds { get; set; } = [];
    public List<TestLamp> Lamps { get; set; } = [];
}
