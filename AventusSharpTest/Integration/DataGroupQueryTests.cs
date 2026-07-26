using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataGroupQueryTests
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
            NewDevice("Office online", "Office", true),
            NewDevice("Office offline", "Office", false),
            NewDevice("Kitchen online", "Kitchen", true),
            NewDevice("Kitchen online 2", "Kitchen", true)
        });
    }

    [Test]
    public async Task Group_returns_one_projected_row_per_distinct_value()
    {
        var result = await Device.StartQuery()
            .Field(device => device.Room)
            .Group(device => device.Room)
            .Sort(device => device.Room, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Room),
            Is.EqualTo(new[] { "Kitchen", "Office" }));
    }

    [Test]
    public async Task Multiple_Group_calls_create_composite_groups_after_filtering()
    {
        var result = await Device.StartQuery()
            .Field(device => device.Room)
            .Field(device => device.IsOnline)
            .Where(device => device.Brightness >= 10)
            .Group(device => device.Room)
            .Group(device => device.IsOnline)
            .Sort(device => device.Room, Sort.ASC)
            .Sort(device => device.IsOnline, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => (device.Room, device.IsOnline)),
            Is.EqualTo(new[]
            {
                ("Kitchen", true),
                ("Office", false),
                ("Office", true)
            }));
    }

    private static Device NewDevice(string name, string room, bool online) =>
        new()
        {
            Name = name,
            Room = room,
            Brightness = 10,
            PowerConsumption = 1,
            IsOnline = online,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 5, 6)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 5, 6, 7, 8, 9))
        };
}
