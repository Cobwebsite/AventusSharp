using AventusSharp.Data;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataLambdaAdvancedTests
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
    public async Task Arithmetic_operators_can_be_combined_in_a_predicate()
    {
        await Seed();

        var result = await Device.StartQuery()
            .Where(device => device.Brightness + 5 >= 25
                && device.Brightness * 2 < 70
                && device.Brightness - 10 > 0)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Middle" }));
    }

    [Test]
    public async Task Date_components_can_be_used_in_a_predicate()
    {
        await Seed();

        var result = await Device.StartQuery()
            .Where(device => device.InstalledOn.Year == 2026
                && device.InstalledOn.Month == 7
                && device.InstalledOn.Day == 25)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Middle" }));
    }

    [Test]
    public async Task ToUpper_is_translated_like_ToLower()
    {
        await Seed();

        var result = await Device.StartQuery()
            .Where(device => device.Name.ToUpper() == "MIDDLE")
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Middle" }));
    }

    [Test]
    public async Task Empty_captured_collection_returns_no_rows()
    {
        await Seed();
        var rooms = new List<string>();

        var result = await Device.StartQuery()
            .Where(device => rooms.Contains(device.Room))
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Is.Empty);
    }

    [Test]
    public async Task A_captured_value_can_be_reused_in_the_same_expression()
    {
        await Seed();
        var threshold = 20;

        var result = await Device.StartQuery()
            .Where(device => device.Brightness >= threshold
                && device.Brightness <= threshold)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Middle" }));
    }

    [Test]
    public async Task Division_operator_can_be_used_in_a_predicate()
    {
        await Seed();
        var divisor = 2;

        var result = await Device.StartQuery()
            .Where(device => device.Brightness / divisor == 10)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Middle" }));
    }

    [Test]
    public async Task Time_components_can_be_used_in_a_predicate()
    {
        await Seed();

        var result = await Device.StartQuery()
            .Where(device => device.LastSeen.Hour == 14
                && device.LastSeen.Minute == 35
                && device.LastSeen.Second == 42)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Middle" }));
    }

    [Test]
    public async Task Numeric_functions_can_be_used_in_predicates()
    {
        await Seed();

        var absolute = await Device.StartQuery()
            .Where(device => Math.Abs(device.PowerConsumption) == 1d)
            .RunWithError();
        var rounded = await Device.StartQuery()
            .Where(device => Math.Round(device.PowerConsumption) == 2d)
            .RunWithError();
        var bounded = await Device.StartQuery()
            .Where(device => Math.Floor(device.PowerConsumption) == 2d
                && Math.Ceiling(device.PowerConsumption) == 3d)
            .RunWithError();

        Assert.That(absolute.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(absolute.Errors));
        Assert.That(rounded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(rounded.Errors));
        Assert.That(bounded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(bounded.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(absolute.Result!.Select(device => device.Name),
                Is.EqualTo(new[] { "Low" }));
            Assert.That(rounded.Result!.Select(device => device.Name),
                Is.EqualTo(new[] { "Middle" }));
            Assert.That(bounded.Result!.Select(device => device.Name),
                Is.EqualTo(new[] { "Middle" }));
        });
    }

    [Test]
    public async Task Numeric_function_preserves_a_nested_arithmetic_expression()
    {
        await Seed();

        var result = await Device.StartQuery()
            .Where(device => Math.Abs(device.PowerConsumption - 3d) == 1d)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "High" }));
    }

    [Test]
    public async Task String_literals_with_quotes_are_escaped_in_sql()
    {
        await Device.Create(NewDevice(
            "O'Reilly lamp",
            "Owner's office",
            10,
            new DateTime(2026, 1, 1)));

        var equality = await Device.StartQuery()
            .Where(device => device.Name == "O'Reilly lamp")
            .RunWithError();
        var startsWith = await Device.StartQuery()
            .Where(device => device.Room.StartsWith("Owner's"))
            .RunWithError();

        Assert.That(equality.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(equality.Errors));
        Assert.That(startsWith.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(startsWith.Errors));
        Assert.That(equality.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "O'Reilly lamp" }));
        Assert.That(startsWith.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "O'Reilly lamp" }));
    }

    [Test]
    public async Task Captured_strings_with_quotes_are_safely_translated()
    {
        await Device.Create(NewDevice(
            "O'Reilly lamp",
            "Office",
            10,
            new DateTime(2026, 1, 1)));
        var expectedName = "O'Reilly lamp";

        var result = await Device.StartQuery()
            .Where(device => device.Name == expectedName)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { expectedName }));
    }

    [Test]
    public async Task Captured_string_collection_escapes_quotes_and_ignores_duplicates()
    {
        await Device.BulkCreate(new List<Device>
        {
            NewDevice("O'Reilly", "Office", 10, new DateTime(2026, 1, 1)),
            NewDevice("Other", "Office", 20, new DateTime(2026, 1, 1))
        });
        var accepted = new List<string> { "O'Reilly", "O'Reilly" };

        var result = await Device.StartQuery()
            .Where(device => accepted.Contains(device.Name))
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "O'Reilly" }));
    }

    private static async Task Seed()
    {
        await Device.BulkCreate(new List<Device>
        {
            NewDevice("Low", "Office", 10, new DateTime(2024, 1, 1)),
            NewDevice("Middle", "Kitchen", 20, new DateTime(2026, 7, 25, 14, 35, 42)),
            NewDevice("High", "Cellar", 40, new DateTime(2026, 7, 26))
        });
    }

    private static Device NewDevice(
        string name,
        string room,
        int brightness,
        DateTime installedOn) =>
        new()
        {
            Name = name,
            Room = room,
            Brightness = brightness,
            PowerConsumption = brightness switch
            {
                10 => -1d,
                20 => 2.4d,
                _ => brightness / 10d
            },
            IsOnline = true,
            InstalledOn = new Date(installedOn),
            LastSeen = new Datetime(installedOn)
        };
}
