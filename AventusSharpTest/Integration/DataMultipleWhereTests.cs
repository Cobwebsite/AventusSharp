using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataMultipleWhereTests
{
    [SetUp]
    public async Task SetUp()
    {
        var clear = await Device.StartDelete()
            .Where(device => device.Id > 0)
            .RunWithError();
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        await Device.BulkCreate(new List<Device>
        {
            NewDevice("Office low", "Office", 10, true),
            NewDevice("Office high", "Office", 80, false),
            NewDevice("Kitchen high", "Kitchen", 80, true)
        });
    }

    [Test]
    public async Task Multiple_Where_calls_are_combined_with_and()
    {
        var result = await Device.StartQuery()
            .Where(device => device.Room == "Office")
            .Where(device => device.Brightness >= 50)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Office high" }));
    }

    [Test]
    public async Task OrWhere_combines_the_previous_filter_with_or()
    {
        var result = await Device.StartQuery()
            .Where(device => device.Brightness < 20)
            .OrWhere(device => device.Room == "Kitchen")
            .Sort(device => device.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Kitchen high", "Office low" }));
    }

    [Test]
    public async Task Where_and_OrWhere_are_grouped_from_left_to_right()
    {
        var result = await Device.StartQuery()
            .Where(device => device.Room == "Office")
            .OrWhere(device => device.Brightness >= 80)
            .Where(device => device.IsOnline)
            .Sort(device => device.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Kitchen high", "Office low" }));
    }

    [Test]
    public async Task Exist_builder_supports_multiple_where_branches()
    {
        var result = await Device.StartExist()
            .Where(device => device.Name == "missing")
            .OrWhere(device => device.Room == "Kitchen")
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.True);
    }

    [Test]
    public async Task Update_and_delete_builders_support_composed_where_branches()
    {
        var update = await Device.StartUpdate()
            .Field(device => device.IsOnline)
            .Where(device => device.Room == "Office")
            .Where(device => device.Brightness < 20)
            .RunWithError(new Device { IsOnline = false });
        var deletion = await Device.StartDelete()
            .Where(device => device.Name == "Office high")
            .OrWhere(device => device.Room == "Kitchen")
            .RunWithError();
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Name\", \"IsOnline\" FROM \"devices\" ORDER BY \"Name\";");

        Assert.That(update.Success, Is.True, IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(update.Result, Has.Count.EqualTo(1));
        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(deletion.Result, Has.Count.EqualTo(2));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result, Has.Count.EqualTo(1));
        Assert.That(rows.Result![0]["Name"], Is.EqualTo("Office low"));
        Assert.That(rows.Result[0]["IsOnline"], Is.EqualTo("0"));
    }

    private static Device NewDevice(
        string name,
        string room,
        int brightness,
        bool isOnline) =>
        new()
        {
            Name = name,
            Room = room,
            Brightness = brightness,
            PowerConsumption = brightness / 10d,
            IsOnline = isOnline,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 4, 5)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 4, 5, 6, 7, 8))
        };
}
