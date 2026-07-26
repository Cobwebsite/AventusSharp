using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataPreparedQueryTests
{
    [SetUp]
    public async Task ClearTable()
    {
        var result = await Device.StartDelete()
            .Where(device => device.Id > 0)
            .RunWithError();
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));

        await Device.BulkCreate(new List<Device>
        {
            NewDevice("Low", 10),
            NewDevice("Medium", 50),
            NewDevice("High", 90)
        });
    }

    [Test]
    public async Task Prepared_query_can_be_reused_with_different_values()
    {
        var minimum = 0;
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => device.Brightness >= minimum)
            .Sort(device => device.Brightness, Sort.ASC);

        var first = await prepared.New().Prepare(40).RunWithError();
        var second = await prepared.New().Prepare(80).RunWithError();

        Assert.That(first.Success, Is.True, IntegrationEnvironment.ErrorMessages(first.Errors));
        Assert.That(first.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Medium", "High" }));
        Assert.That(second.Success, Is.True, IntegrationEnvironment.ErrorMessages(second.Errors));
        Assert.That(second.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "High" }));
    }

    [Test]
    public async Task Prepared_query_supports_setting_a_variable_by_name()
    {
        var minimum = 0;
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => device.Brightness >= minimum)
            .Sort(device => device.Brightness, Sort.ASC);

        var result = await prepared.New()
            .SetVariables(set => set(nameof(minimum), 50))
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Medium", "High" }));
    }

    [Test]
    public async Task Missing_prepared_parameter_fails_at_run_without_poisoning_the_builder()
    {
        var minimum = 0;
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => device.Brightness >= minimum)
            .Sort(device => device.Brightness, Sort.ASC);

        var missing = await prepared.New().RunWithError();
        var valid = await prepared.New().Prepare(50).RunWithError();

        Assert.That(missing.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(missing.Errors),
            Does.Contain(nameof(minimum)));
        Assert.That(valid.Success, Is.True, IntegrationEnvironment.ErrorMessages(valid.Errors));
        Assert.That(valid.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Medium", "High" }));
    }

    [Test]
    public async Task New_prepared_execution_does_not_reuse_the_previous_parameter_value()
    {
        var minimum = 0;
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => device.Brightness >= minimum);

        var valid = await prepared.New().Prepare(50).RunWithError();
        var missing = await prepared.New().RunWithError();

        Assert.That(valid.Success, Is.True, IntegrationEnvironment.ErrorMessages(valid.Errors));
        Assert.That(missing.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(missing.Errors),
            Does.Contain(nameof(minimum)));
    }

    [Test]
    public async Task Partially_named_prepared_query_reports_the_remaining_parameter()
    {
        var minimum = 0;
        var maximum = 0;
        var prepared = Device.StartQuery()
            .WhereWithParameters(device =>
                device.Brightness >= minimum && device.Brightness <= maximum);

        var partial = await prepared.New()
            .SetVariables(set => set(nameof(minimum), 20))
            .RunWithError();
        var valid = await prepared.New()
            .SetVariables(set =>
            {
                set(nameof(minimum), 20);
                set(nameof(maximum), 80);
            })
            .RunWithError();

        Assert.That(partial.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(partial.Errors),
            Does.Contain(nameof(maximum)));
        Assert.That(valid.Success, Is.True, IntegrationEnvironment.ErrorMessages(valid.Errors));
        Assert.That(valid.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Medium" }));
    }

    [Test]
    public async Task Prepared_query_with_an_incompatible_value_fails_at_run_and_releases_the_lock()
    {
        var minimum = 0;
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => device.Brightness >= minimum);

        var incompatible = await prepared.New().Prepare("not an integer").RunWithError();
        var valid = await prepared.New().Prepare(80).RunWithError();

        Assert.That(incompatible.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(incompatible.Errors),
            Does.Contain(nameof(minimum)));
        Assert.That(valid.Success, Is.True, IntegrationEnvironment.ErrorMessages(valid.Errors));
        Assert.That(valid.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "High" }));
    }

    [Test]
    public async Task Prepared_exist_query_can_be_reused_with_different_values()
    {
        var minimum = 0;
        var prepared = Device.StartExist()
            .WhereWithParameters(device => device.Brightness >= minimum);

        var exists = await prepared.New().Prepare(90).RunWithError();
        var doesNotExist = await prepared.New().Prepare(100).RunWithError();

        Assert.That(exists.Success, Is.True, IntegrationEnvironment.ErrorMessages(exists.Errors));
        Assert.That(exists.Result, Is.True);
        Assert.That(doesNotExist.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(doesNotExist.Errors));
        Assert.That(doesNotExist.Result, Is.False);
    }

    [Test]
    public async Task Missing_prepared_exist_parameter_fails_at_run()
    {
        var minimum = 0;
        var prepared = Device.StartExist()
            .WhereWithParameters(device => device.Brightness >= minimum);

        var result = await prepared.New().RunWithError();

        Assert.That(result.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(result.Errors),
            Does.Contain(nameof(minimum)));
    }

    [Test]
    public async Task Prepared_update_only_changes_rows_matching_the_current_value()
    {
        var minimum = 0;
        var prepared = Device.StartUpdate()
            .Field(device => device.IsOnline)
            .WhereWithParameters(device => device.Brightness >= minimum);

        var update = await prepared.New()
            .Prepare(80)
            .RunWithError(new Device { IsOnline = false });
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Name\", \"IsOnline\" FROM \"devices\" ORDER BY \"Brightness\";");

        Assert.That(update.Success, Is.True, IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(update.Result, Has.Count.EqualTo(1));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(row => row["IsOnline"]),
            Is.EqualTo(new[] { "1", "1", "0" }));
    }

    [Test]
    public async Task Missing_prepared_update_parameter_does_not_modify_rows()
    {
        var minimum = 0;
        var prepared = Device.StartUpdate()
            .Field(device => device.IsOnline)
            .WhereWithParameters(device => device.Brightness >= minimum);

        var update = await prepared.New()
            .RunWithError(new Device { IsOnline = false });
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT \"IsOnline\" FROM \"devices\" ORDER BY \"Brightness\";");

        Assert.That(update.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(update.Errors),
            Does.Contain(nameof(minimum)));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(row => row["IsOnline"]),
            Is.EqualTo(new[] { "1", "1", "1" }));
    }

    [Test]
    public async Task Prepared_delete_only_removes_rows_matching_the_current_value()
    {
        var minimum = 0;
        var prepared = Device.StartDelete()
            .WhereWithParameters(device => device.Brightness >= minimum);

        var deletion = await prepared.New().Prepare(80).RunWithError();
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Name\" FROM \"devices\" ORDER BY \"Brightness\";");

        Assert.That(deletion.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(deletion.Result, Has.Count.EqualTo(1));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(row => row["Name"]),
            Is.EqualTo(new[] { "Low", "Medium" }));
    }

    [Test]
    public async Task Missing_prepared_delete_parameter_does_not_remove_rows()
    {
        var minimum = 0;
        var prepared = Device.StartDelete()
            .WhereWithParameters(device => device.Brightness >= minimum);

        var deletion = await prepared.New().RunWithError();
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Name\" FROM \"devices\" ORDER BY \"Brightness\";");

        Assert.That(deletion.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(deletion.Errors),
            Does.Contain(nameof(minimum)));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(row => row["Name"]),
            Is.EqualTo(new[] { "Low", "Medium", "High" }));
    }

    [Test]
    public async Task Concurrent_prepared_executions_do_not_mix_parameter_values()
    {
        var minimum = 0;
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => device.Brightness >= minimum)
            .Sort(device => device.Brightness, Sort.ASC);

        Task<AventusSharp.Tools.ResultWithError<List<Device>>> Run(int value) =>
            Task.Run(async () => await prepared.New().Prepare(value).RunWithError());

        var results = await Task.WhenAll(Run(40), Run(80));
        var returnedNames = results
            .Select(result => string.Join(",", result.Result!.Select(device => device.Name)))
            .ToArray();

        Assert.That(results, Has.All.Property(nameof(AventusSharp.Tools.IWithError.Success)).True);
        Assert.That(returnedNames,
            Is.EquivalentTo(new[] { "Medium,High", "High" }));
    }

    [Test]
    public async Task Prepared_builder_releases_its_lock_after_an_error_result()
    {
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => device.Name.Trim() == "unsupported");

        var first = await prepared.New().RunWithError();
        var secondTask = Task.Run(async () => await prepared.New().RunWithError());
        var completed = await Task.WhenAny(secondTask, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.That(first.Success, Is.False);
        Assert.That(completed, Is.SameAs(secondTask),
            "An error result must release the prepared builder semaphore.");
        var second = await secondTask;
        Assert.That(second.Success, Is.False);
    }

    [Test]
    public async Task Prepared_query_maps_multiple_values_of_the_same_type_in_expression_order()
    {
        var minimum = 0;
        var maximum = 0;
        var prepared = Device.StartQuery()
            .WhereWithParameters(device =>
                device.Brightness >= minimum && device.Brightness <= maximum)
            .Sort(device => device.Brightness, Sort.ASC);

        var result = await prepared.New().Prepare(20, 80).RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Medium" }));
    }

    [Test]
    public async Task Prepared_query_preserves_non_storable_collection_values_for_contains()
    {
        var names = new List<string>();
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => names.Contains(device.Name))
            .Sort(device => device.Brightness, Sort.ASC);

        var result = await prepared.New()
            .Prepare(new List<string> { "Low", "High" })
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Low", "High" }));
    }

    [Test]
    public async Task Prepared_contains_can_be_reused_with_a_non_empty_then_empty_collection()
    {
        var names = new List<string>();
        var prepared = Device.StartQuery()
            .WhereWithParameters(device => names.Contains(device.Name))
            .Sort(device => device.Brightness, Sort.ASC);

        var nonEmpty = await prepared.New()
            .Prepare(new List<string> { "Medium" })
            .RunWithError();
        var empty = await prepared.New()
            .Prepare(new List<string>())
            .RunWithError();

        Assert.That(nonEmpty.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(nonEmpty.Errors));
        Assert.That(nonEmpty.Result!.Select(device => device.Name),
            Is.EqualTo(new[] { "Medium" }));
        Assert.That(empty.Success, Is.True, IntegrationEnvironment.ErrorMessages(empty.Errors));
        Assert.That(empty.Result, Is.Empty);
    }

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
