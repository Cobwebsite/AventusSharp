using AventusSharp.Data.Manager.DB;
using AventusSharp.Tools;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataQueryStreamTests
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
    public async Task Stream_visits_each_matching_row_in_query_order()
    {
        await SeedDevices();
        var visited = new List<string>();

        var result = await Device.StartQuery()
            .Where(device => device.Brightness >= 20)
            .Sort(device => device.Brightness, Sort.DESC)
            .RunStreamWithError(device =>
            {
                visited.Add(device.Name);
                return Task.FromResult(new VoidWithError());
            });

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(visited, Is.EqualTo(new[] { "High", "Medium" }));
    }

    [Test]
    public async Task Stream_with_no_matching_rows_does_not_invoke_the_callback()
    {
        await SeedDevices();
        var calls = 0;

        var result = await Device.StartQuery()
            .Where(device => device.Brightness > 1_000)
            .RunStreamWithError(_ =>
            {
                calls++;
                return Task.FromResult(new VoidWithError());
            });

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public async Task Stream_propagates_errors_returned_by_the_callback()
    {
        await SeedDevices();
        var calls = 0;

        var result = await Device.StartQuery()
            .Sort(device => device.Brightness, Sort.ASC)
            .RunStreamWithError(_ =>
            {
                calls++;
                return Task.FromResult(new VoidWithError
                {
                    Errors = [new GenericError(9920, "stream callback failed")]
                });
            });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Message),
            Does.Contain("stream callback failed"));
        Assert.That(calls, Is.EqualTo(1),
            "Streaming must stop after the callback reports an error.");
    }

    [Test]
    public async Task Stream_converts_callback_exceptions_and_releases_database_resources()
    {
        await SeedDevices();

        var result = await Device.StartQuery()
            .Sort(device => device.Brightness, Sort.ASC)
            .RunStreamWithError(_ =>
                throw new InvalidOperationException("stream callback exception"));
        var queryAfterFailure = await Device.StartQuery()
            .Where(device => device.Brightness >= 0)
            .RunWithError();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Message),
            Has.Some.Contains("stream callback exception"));
        Assert.That(queryAfterFailure.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(queryAfterFailure.Errors));
        Assert.That(queryAfterFailure.Result, Has.Count.EqualTo(3),
            "The reader and transaction must be released after a callback exception.");
    }

    private static Task<bool> SeedDevices() =>
        Device.BulkCreate(new List<Device>
        {
            NewDevice("Low", 10),
            NewDevice("Medium", 50),
            NewDevice("High", 90)
        });

    private static Device NewDevice(string name, int brightness) =>
        new()
        {
            Name = name,
            Room = "Lab",
            Brightness = brightness,
            PowerConsumption = brightness / 10d,
            IsOnline = true,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 1, 2)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 1, 2, 3, 4, 5))
        };
}
