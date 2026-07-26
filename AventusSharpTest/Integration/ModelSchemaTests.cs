using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class ModelSchemaTests
{
    [Test]
    public async Task Initialization_creates_table_from_model()
    {
        var table = await IntegrationEnvironment.Storage.Query(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='devices';");

        Assert.That(table.Success, Is.True, IntegrationEnvironment.ErrorMessages(table.Errors));
        Assert.That(table.Result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Generated_table_contains_expected_columns_and_excludes_NotInDB()
    {
        var columns = await IntegrationEnvironment.Storage.Query("PRAGMA table_info('devices');");
        Assert.That(columns.Success, Is.True, IntegrationEnvironment.ErrorMessages(columns.Errors));

        var names = columns.Result!.Select(row => row["name"]).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("Id"));
            Assert.That(names, Does.Contain("Name"));
            Assert.That(names, Does.Contain("Room"));
            Assert.That(names, Does.Contain("Brightness"));
            Assert.That(names, Does.Contain("PowerConsumption"));
            Assert.That(names, Does.Contain("IsOnline"));
            Assert.That(names, Does.Contain("InstalledOn"));
            Assert.That(names, Does.Contain("LastSeen"));
            Assert.That(names, Does.Not.Contain(nameof(Device.RuntimeState)));
        });
    }

    [Test]
    public async Task Generated_column_types_follow_the_model()
    {
        var columns = await IntegrationEnvironment.Storage.Query("PRAGMA table_info('devices');");
        var types = columns.Result!.ToDictionary(row => row["name"]!, row => row["type"]!);

        Assert.Multiple(() =>
        {
            Assert.That(types["Id"], Is.EqualTo("INTEGER"));
            Assert.That(types["Name"], Does.StartWith("varchar"));
            Assert.That(types["Brightness"], Is.EqualTo("INT"));
            Assert.That(types["PowerConsumption"], Is.EqualTo("float").IgnoreCase);
            Assert.That(types["InstalledOn"], Is.EqualTo("date").IgnoreCase);
            Assert.That(types["LastSeen"], Is.EqualTo("datetime").IgnoreCase);
        });
    }
}
