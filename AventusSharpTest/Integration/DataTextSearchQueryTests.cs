using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataTextSearchQueryTests
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
            NewDevice("Desk lamp", "Office", 25, true),
            NewDevice("Kitchen's main light", "Kitchen", 80, false),
            NewDevice("Hall sensor", "Hall", 50, true)
        });
    }

    [Test]
    public async Task Text_search_combines_selected_string_fields_with_or()
    {
        var result = await Device.StartQuery()
            .Where("Hall", [nameof(Device.Name), nameof(Device.Room)])
            .Sort(device => device.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Hall sensor" }));
    }

    [Test]
    public async Task Text_search_converts_numeric_and_boolean_values()
    {
        var numeric = await Device.StartQuery()
            .Where("80", [nameof(Device.Brightness)])
            .RunWithError();
        var boolean = await Device.StartQuery()
            .Where("False", [nameof(Device.IsOnline)])
            .RunWithError();

        Assert.That(numeric.Success, Is.True, IntegrationEnvironment.ErrorMessages(numeric.Errors));
        Assert.That(numeric.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Kitchen's main light" }));
        Assert.That(boolean.Success, Is.True, IntegrationEnvironment.ErrorMessages(boolean.Errors));
        Assert.That(boolean.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Kitchen's main light" }));
    }

    [Test]
    public async Task Text_search_escapes_apostrophes()
    {
        var result = await Device.StartQuery()
            .Where("Kitchen's", [nameof(Device.Name)])
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Kitchen's main light" }));
    }

    [Test]
    public async Task Invalid_field_name_returns_an_error_result()
    {
        var result = await Device.StartQuery()
            .Where("value", ["MissingField"])
            .RunWithError();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Message),
            Has.Some.Contains("MissingField"));
    }

    [Test]
    public async Task Unconvertible_search_value_returns_an_error_at_execution()
    {
        var result = await Device.StartQuery()
            .Where("not-a-number", [nameof(Device.Brightness)])
            .RunWithError();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Message),
            Has.Some.Contains("not-a-number"));
        Assert.That(result.Errors.Select(error => error.Message),
            Has.Some.Contains(nameof(Device)));
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
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 3, 4)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 3, 4, 5, 6, 7))
        };
}
