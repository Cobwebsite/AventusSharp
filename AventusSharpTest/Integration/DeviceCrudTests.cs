using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DeviceCrudTests
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
    public async Task Create_assigns_an_id_and_persists_all_database_fields()
    {
        var device = NewDevice("Desk lamp", "Office", 35);

        var created = await Device.CreateWithError(device);
        var row = await IntegrationEnvironment.Storage.Query(
            $"SELECT * FROM devices WHERE Id = {device.Id};");

        Assert.That(created.Success, Is.True, IntegrationEnvironment.ErrorMessages(created.Errors));
        Assert.That(device.Id, Is.GreaterThan(0));
        Assert.That(row.Result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(row.Result![0]["Name"], Is.EqualTo("Desk lamp"));
            Assert.That(row.Result[0]["Brightness"], Is.EqualTo("35"));
            Assert.That(row.Result[0].ContainsKey(nameof(Device.RuntimeState)), Is.False);
        });
    }

    [Test]
    public async Task GetById_returns_the_same_instance_when_local_cache_is_enabled()
    {
        var created = await Device.Create(NewDevice("Hall light", "Hall", 20));
        Assert.That(created, Is.Not.Null);

        var first = await Device.GetById(created!.Id);
        var second = await Device.GetById(created.Id);

        Assert.That(first, Is.SameAs(created));
        Assert.That(second, Is.SameAs(created));
    }

    [Test]
    public async Task Cached_runtime_state_survives_database_queries()
    {
        var created = await Device.Create(NewDevice("Living light", "Living", 60));
        created!.RuntimeState = "manually-overridden";

        var queried = await Device.Single(device => device.Id == created.Id);

        Assert.That(queried, Is.SameAs(created));
        Assert.That(queried!.RuntimeState, Is.EqualTo("manually-overridden"));
    }

    [Test]
    public async Task Update_persists_changes_without_replacing_cached_instance()
    {
        var device = (await Device.Create(NewDevice("Kitchen", "Kitchen", 40)))!;
        device.Brightness = 90;
        device.IsOnline = false;

        var updateErrors = await device.UpdateWithError();
        var loaded = await Device.GetById(device.Id);

        Assert.That(updateErrors, Is.Empty, IntegrationEnvironment.ErrorMessages(updateErrors));
        Assert.That(loaded, Is.SameAs(device));
        Assert.That(loaded!.Brightness, Is.EqualTo(90));
        Assert.That(loaded.IsOnline, Is.False);
    }

    [Test]
    public async Task Delete_removes_database_row_and_cached_identity()
    {
        var device = (await Device.Create(NewDevice("Temporary", "Lab", 10)))!;
        var id = device.Id;

        var deleteErrors = await device.DeleteWithError();
        var loaded = await Device.GetById(id);

        Assert.That(deleteErrors, Is.Empty, IntegrationEnvironment.ErrorMessages(deleteErrors));
        Assert.That(loaded, Is.Null);
    }

    [Test]
    public async Task BulkCreate_inserts_all_items()
    {
        var devices = new List<Device>
        {
            NewDevice("One", "Office", 10),
            NewDevice("Two", "Office", 20),
            NewDevice("Three", "Kitchen", 30)
        };

        var result = await Device.BulkCreateWithError(devices);
        var loaded = await Device.GetAll();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(loaded, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Query_builder_supports_filter_sort_limit_and_offset()
    {
        await Device.BulkCreate(new List<Device>
        {
            NewDevice("A", "Office", 10),
            NewDevice("B", "Office", 30),
            NewDevice("C", "Office", 20),
            NewDevice("D", "Kitchen", 100)
        });

        var result = await Device.StartQuery()
            .Where(device => device.Room == "Office" && device.Brightness >= 10)
            .Sort(device => device.Brightness, Sort.DESC)
            .Take(2, 1)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name), Is.EqualTo(new[] { "C", "A" }));
    }

    [TestCase("Desk", true)]
    [TestCase("desk", true)]
    public async Task LambdaTranslator_supports_string_contains(string search, bool expected)
    {
        await Device.Create(NewDevice("Desk lamp", "Office", 30));

        var exists = await Device.StartExist()
            .Where(device => device.Name.Contains(search))
            .RunWithError();

        Assert.That(exists.Success, Is.True, IntegrationEnvironment.ErrorMessages(exists.Errors));
        Assert.That(exists.Result, Is.EqualTo(expected));
    }

    [Test]
    public async Task LambdaTranslator_supports_captured_variables_and_boolean_members()
    {
        await Device.Create(NewDevice("Low", "Office", 20));
        await Device.Create(NewDevice("High", "Office", 80));
        await Device.Create(NewDevice("Offline", "Office", 90, false));
        var minimum = 50;
        var room = "Office";

        var result = await Device.Where(
            device => device.Room == room && device.Brightness >= minimum && device.IsOnline);

        var names = result.Select(device => device.Name).ToArray();
        Assert.That(
            names,
            Is.EqualTo(new[] { "High" }),
            $"Returned devices: {string.Join(", ", names)}");
    }

    [Test]
    public async Task LambdaTranslator_supports_or_and_negation()
    {
        await Device.Create(NewDevice("Office light", "Office", 20));
        await Device.Create(NewDevice("Kitchen light", "Kitchen", 80));
        await Device.Create(NewDevice("Offline light", "Cellar", 90, false));

        var result = await Device.Where(device =>
            (device.Room == "Office" || device.Brightness >= 80) && !device.IsOnline == false);

        Assert.That(
            result.Select(device => device.Name).OrderBy(name => name),
            Is.EqualTo(new[] { "Kitchen light", "Office light" }));
    }

    [Test]
    public async Task LambdaTranslator_supports_startswith_endswith_and_tolower()
    {
        await Device.Create(NewDevice("Desk Lamp", "Office", 20));
        await Device.Create(NewDevice("Ceiling Lamp", "Office", 30));
        await Device.Create(NewDevice("Desk Sensor", "Office", 40));

        var starts = await Device.Where(device => device.Name.StartsWith("Desk"));
        var ends = await Device.Where(device => device.Name.EndsWith("Lamp"));
        var lower = await Device.Where(device => device.Name.ToLower() == "desk lamp");

        Assert.Multiple(() =>
        {
            Assert.That(starts.Select(device => device.Name), Is.EquivalentTo(new[] { "Desk Lamp", "Desk Sensor" }));
            Assert.That(ends.Select(device => device.Name), Is.EquivalentTo(new[] { "Desk Lamp", "Ceiling Lamp" }));
            Assert.That(lower.Select(device => device.Name), Is.EqualTo(new[] { "Desk Lamp" }));
        });
    }

    [Test]
    public async Task LambdaTranslator_supports_collection_contains()
    {
        await Device.Create(NewDevice("One", "Office", 10));
        await Device.Create(NewDevice("Two", "Kitchen", 20));
        await Device.Create(NewDevice("Three", "Cellar", 30));
        var rooms = new List<string> { "Office", "Kitchen" };

        var result = await Device.Where(device => rooms.Contains(device.Room));

        Assert.That(result.Select(device => device.Name), Is.EquivalentTo(new[] { "One", "Two" }));
    }

    [Test]
    public async Task LambdaTranslator_supports_date_and_datetime_comparisons()
    {
        var old = NewDevice("Old", "Office", 10);
        old.InstalledOn = new AventusSharp.Data.Date(new DateTime(2020, 1, 1));
        var recent = NewDevice("Recent", "Office", 20);
        recent.InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 1, 1));
        await Device.Create(old);
        await Device.Create(recent);
        var threshold = new DateTime(2025, 1, 1);

        var result = await Device.Where(device => device.InstalledOn >= threshold);

        Assert.That(result.Select(device => device.Name), Is.EqualTo(new[] { "Recent" }));
    }

    [Test]
    public async Task ExistBuilder_returns_true_and_false_with_captured_values()
    {
        await Device.Create(NewDevice("Present", "Office", 25));
        var room = "Office";

        var present = await Device.StartExist()
            .Where(device => device.Room == room && device.Brightness >= 20)
            .RunWithError();
        room = "Missing";
        var missing = await Device.StartExist()
            .Where(device => device.Room == room)
            .RunWithError();

        Assert.Multiple(() =>
        {
            Assert.That(present.Success, Is.True, IntegrationEnvironment.ErrorMessages(present.Errors));
            Assert.That(present.Result, Is.True);
            Assert.That(missing.Success, Is.True, IntegrationEnvironment.ErrorMessages(missing.Errors));
            Assert.That(missing.Result, Is.False);
        });
    }

    [Test]
    public async Task UpdateBuilder_updates_only_selected_fields_and_matching_rows()
    {
        await Device.Create(NewDevice("Target", "Office", 10));
        await Device.Create(NewDevice("Other", "Kitchen", 20));

        var update = await Device.StartUpdate()
            .Field(device => device.Brightness)
            .Where(device => device.Room == "Office")
            .RunWithError(new Device { Brightness = 90 });
        var loaded = await ((DeviceManager)AventusSharp.Data.Manager.GenericDM.Get<Device>())
            .WhereWithErrorNoCache<Device>(device => device.Brightness >= 0);

        Assert.That(update.Success, Is.True, IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Single(device => device.Name == "Target").Brightness, Is.EqualTo(90));
        Assert.That(loaded.Result!.Single(device => device.Name == "Other").Brightness, Is.EqualTo(20));
    }

    [Test]
    public async Task DeleteBuilder_deletes_only_matching_rows()
    {
        await Device.Create(NewDevice("Remove", "Office", 10));
        await Device.Create(NewDevice("Keep", "Kitchen", 20));

        var deletion = await Device.StartDelete()
            .Where(device => device.Room == "Office")
            .RunWithError();
        var loaded = await ((DeviceManager)AventusSharp.Data.Manager.GenericDM.Get<Device>())
            .WhereWithErrorNoCache<Device>(device => device.Brightness >= 0);

        Assert.That(deletion.Success, Is.True, IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Select(device => device.Name), Is.EqualTo(new[] { "Keep" }));
    }

    [Test]
    public async Task Unsupported_lambda_expression_returns_an_error_result()
    {
        var result = await Device.StartQuery()
            .Where(device => device.Name.Trim() == "unsupported")
            .RunWithError();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Is.Not.Empty);
    }

    private static Device NewDevice(
        string name,
        string room,
        int brightness,
        bool isOnline = true)
    {
        return new Device
        {
            Name = name,
            Room = room,
            Brightness = brightness,
            PowerConsumption = brightness / 10d,
            IsOnline = isOnline,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 1, 2)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 1, 2, 3, 4, 5))
        };
    }
}
