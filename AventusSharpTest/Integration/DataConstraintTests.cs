using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataConstraintTests
{
    [SetUp]
    public async Task ClearRooms()
    {
        var dependencies = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_scenes_test_lamps\";" +
            "DELETE FROM \"test_scenes\";" +
            "DELETE FROM \"test_sensors\";" +
            "DELETE FROM \"test_lamps\";");
        Assert.That(dependencies.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(dependencies.Errors));

        var rooms = await TestRoom.StartDelete()
            .Where(room => room.Id > 0)
            .RunWithError();
        Assert.That(rooms.Success, Is.True, IntegrationEnvironment.ErrorMessages(rooms.Errors));
    }

    [Test]
    public async Task Unique_attribute_rejects_a_duplicate_value()
    {
        var first = await TestRoom.CreateWithError(new TestRoom { Name = "Office", Code = "A" });
        var duplicate = await TestRoom.CreateWithError(new TestRoom { Name = "Office", Code = "B" });

        Assert.That(first.Success, Is.True, IntegrationEnvironment.ErrorMessages(first.Errors));
        Assert.That(duplicate.Success, Is.False);
        Assert.That(duplicate.Errors.Select(error => error.Message),
            Has.Some.Contains("unique").IgnoreCase);
    }

    [Test]
    public async Task Unique_violation_in_a_create_list_rolls_back_database_and_cache()
    {
        var first = new TestRoom { Name = "Duplicated batch", Code = "first" };
        var second = new TestRoom { Name = "Duplicated batch", Code = "second" };

        var creation = await TestRoom.CreateWithError([first, second]);
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM \"test_rooms\" WHERE \"Name\" = 'Duplicated batch';");
        var cached = await TestRoom.GetByIdWithError(first.Id);

        Assert.That(creation.Success, Is.False);
        Assert.That(creation.Errors.Select(error => error.Message),
            Has.Some.Contains("unique").IgnoreCase);
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["count"], Is.EqualTo("0"));
        Assert.That(cached.Success, Is.False,
            "The item inserted before the SQL error must also be removed from the local cache.");
        Assert.That(cached.Result, Is.Null);
    }

    [TestCase("")]
    [TestCaseSource(nameof(OverlongNames))]
    public async Task Size_attribute_rejects_values_outside_its_bounds(string name)
    {
        var result = await TestRoom.CreateWithError(new TestRoom { Name = name, Code = "size" });

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task Nullable_attribute_accepts_null()
    {
        var result = await TestRoom.CreateWithError(new TestRoom
        {
            Name = "Bedroom",
            Code = "nullable",
            Description = null
        });

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Index_attribute_generates_an_index()
    {
        var indexes = await IntegrationEnvironment.Storage.Query(
            "SELECT name FROM sqlite_master " +
            "WHERE type = 'index' AND tbl_name = 'test_rooms' AND name = 'IND_Code_test_rooms';");

        Assert.That(indexes.Success, Is.True, IntegrationEnvironment.ErrorMessages(indexes.Errors));
        Assert.That(indexes.Result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Size_and_nullability_attributes_are_reflected_in_the_schema()
    {
        var columns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info('test_rooms');");

        Assert.That(columns.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(columns.Errors));
        var schemaColumns = columns.Result!;
        var name = schemaColumns.Single(column => column["name"] == "Name");
        var code = schemaColumns.Single(column => column["name"] == "Code");
        var description = schemaColumns.Single(column => column["name"] == "Description");

        Assert.Multiple(() =>
        {
            Assert.That(name["type"], Is.EqualTo("varchar(100)").IgnoreCase);
            Assert.That(name["notnull"], Is.EqualTo("1"));
            Assert.That(code["notnull"], Is.EqualTo("1"));
            Assert.That(description["notnull"], Is.EqualTo("0"));
        });
    }

    [Test]
    public async Task Database_schema_enforces_unique_and_not_null_without_the_manager()
    {
        var first = await IntegrationEnvironment.Storage.Execute(
            "INSERT INTO \"test_rooms\" (\"Name\", \"Code\", \"Description\") " +
            "VALUES ('Office', 'first', NULL);");
        var duplicate = await IntegrationEnvironment.Storage.Execute(
            "INSERT INTO \"test_rooms\" (\"Name\", \"Code\", \"Description\") " +
            "VALUES ('Office', 'second', NULL);");
        var missingRequiredValue = await IntegrationEnvironment.Storage.Execute(
            "INSERT INTO \"test_rooms\" (\"Name\", \"Code\", \"Description\") " +
            "VALUES ('Bedroom', NULL, NULL);");

        Assert.That(first.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(first.Errors));
        Assert.That(duplicate.Success, Is.False,
            "The database UNIQUE constraint must reject direct writes.");
        Assert.That(missingRequiredValue.Success, Is.False,
            "The database NOT NULL constraint must reject direct writes.");
    }

    [Test]
    public async Task LambdaTranslator_supports_null_and_not_null_comparisons()
    {
        await TestRoom.Create(new TestRoom { Name = "Null", Code = "null", Description = null });
        await TestRoom.Create(new TestRoom { Name = "Value", Code = "value", Description = "present" });

        var nullDescriptions = await ((TestRoomManager)AventusSharp.Data.Manager.GenericDM.Get<TestRoom>())
            .WhereWithErrorNoCache<TestRoom>(room => room.Description == null);
        var nonNullDescriptions = await ((TestRoomManager)AventusSharp.Data.Manager.GenericDM.Get<TestRoom>())
            .WhereWithErrorNoCache<TestRoom>(room => room.Description != null);

        Assert.That(nullDescriptions.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(nullDescriptions.Errors));
        Assert.That(nonNullDescriptions.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(nonNullDescriptions.Errors));
        Assert.That(nullDescriptions.Result!.Select(room => room.Name), Is.EqualTo(new[] { "Null" }));
        Assert.That(nonNullDescriptions.Result!.Select(room => room.Name), Is.EqualTo(new[] { "Value" }));
    }

    private static IEnumerable<string> OverlongNames()
    {
        yield return new string('x', 101);
    }
}
