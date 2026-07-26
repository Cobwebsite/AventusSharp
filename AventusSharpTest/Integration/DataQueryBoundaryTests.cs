using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataQueryBoundaryTests
{
    [SetUp]
    public async Task ClearTable()
    {
        var result = await Device.StartDelete()
            .Where(device => device.Id > 0)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Limit_zero_returns_an_empty_result()
    {
        await SeedDevices();

        var result = await Device.StartQuery()
            .Sort(device => device.Id, Sort.ASC)
            .Limit(0)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.Empty);
    }

    [Test]
    public async Task Offset_beyond_the_available_rows_returns_an_empty_result()
    {
        await SeedDevices();

        var result = await Device.StartQuery()
            .Sort(device => device.Id, Sort.ASC)
            .Offset(100)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.Empty);
    }

    [Test]
    public async Task Multiple_sorts_are_applied_in_declaration_order()
    {
        await Device.BulkCreate(new List<Device>
        {
            NewDevice("Office low", "Office", 10),
            NewDevice("Kitchen high", "Kitchen", 90),
            NewDevice("Office high", "Office", 80),
            NewDevice("Kitchen low", "Kitchen", 20)
        });

        var result = await Device.StartQuery()
            .Sort(device => device.Room, Sort.ASC)
            .Sort(device => device.Brightness, Sort.DESC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name), Is.EqualTo(new[]
        {
            "Kitchen high",
            "Kitchen low",
            "Office high",
            "Office low"
        }));
    }

    [Test]
    public async Task Query_without_matches_succeeds_with_an_empty_result()
    {
        await SeedDevices();

        var result = await Device.StartQuery()
            .Where(device => device.Room == "Unknown")
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.Empty);
    }

    [Test]
    public async Task The_same_query_builder_can_be_run_more_than_once()
    {
        await SeedDevices();
        var query = Device.StartQuery()
            .Where(device => device.Brightness >= 20)
            .Sort(device => device.Id, Sort.ASC);

        var first = await query.RunWithError();
        var second = await query.RunWithError();

        Assert.That(first.Success, Is.True, IntegrationEnvironment.ErrorMessages(first.Errors));
        Assert.That(second.Success, Is.True, IntegrationEnvironment.ErrorMessages(second.Errors));
        var firstDevices = first.Result!;
        var secondDevices = second.Result!;
        Assert.That(secondDevices.Select(device => device.Id),
            Is.EqualTo(firstDevices.Select(device => device.Id)));
    }

    [Test]
    public async Task Negative_limit_returns_a_builder_error_without_running_invalid_sql()
    {
        await SeedDevices();

        var result = await Device.StartQuery()
            .Limit(-1)
            .RunWithError();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Message),
            Has.Some.Contains("Limit"));
    }

    [Test]
    public async Task Negative_offset_returns_a_builder_error_without_running_invalid_sql()
    {
        await SeedDevices();

        var result = await Device.StartQuery()
            .Offset(-1)
            .RunWithError();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Message),
            Has.Some.Contains("Offset"));
    }

    private static Task<bool> SeedDevices() =>
        Device.BulkCreate(new List<Device>
        {
            NewDevice("First", "Office", 10),
            NewDevice("Second", "Kitchen", 20),
            NewDevice("Third", "Office", 30)
        });

    private static Device NewDevice(string name, string room, int brightness) =>
        new()
        {
            Name = name,
            Room = room,
            Brightness = brightness,
            PowerConsumption = brightness / 10d,
            IsOnline = true,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 1, 2)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 1, 2, 3, 4, 5))
        };
}
