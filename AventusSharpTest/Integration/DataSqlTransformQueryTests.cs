using AventusSharp.Data.Manager;
using System.Data;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataSqlTransformQueryTests
{
    [SetUp]
    public async Task SetUp()
    {
        foreach (string table in new[]
        {
            "transformed_bool_records",
            "transformed_number_records",
            "throwing_query_transform_records"
        })
        {
            var reset = await IntegrationEnvironment.Storage.Execute(
                $"DELETE FROM \"{table}\";");
            Assert.That(reset.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(reset.Errors));
        }
    }

    [Test]
    public async Task Boolean_transform_round_trips_as_yes_and_no()
    {
        var active = await TransformedBoolRecord.Create(new TransformedBoolRecord
        {
            Name = "Active",
            Deleted = false
        });
        var deleted = await TransformedBoolRecord.Create(new TransformedBoolRecord
        {
            Name = "Deleted",
            Deleted = true
        });
        var raw = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Name\", \"Deleted\" FROM \"transformed_bool_records\" ORDER BY \"Name\";");
        var manager = (TransformedBoolRecordManager)GenericDM.Get<TransformedBoolRecord>();
        var loadedActive = await manager.GetByIdWithErrorNoCache<TransformedBoolRecord>(active!.Id);
        var loadedDeleted = await manager.GetByIdWithErrorNoCache<TransformedBoolRecord>(deleted!.Id);

        Assert.That(raw.Success, Is.True, IntegrationEnvironment.ErrorMessages(raw.Errors));
        Assert.That(raw.Result!.Single(row => row["Name"] == "Active")["Deleted"], Is.EqualTo("N"));
        Assert.That(raw.Result!.Single(row => row["Name"] == "Deleted")["Deleted"], Is.EqualTo("Y"));
        Assert.That(loadedActive.Result!.Deleted, Is.False);
        Assert.That(loadedDeleted.Result!.Deleted, Is.True);
    }

    [Test]
    public async Task Typed_command_query_maps_each_parameter_set_and_applies_FromSql()
    {
        await Seed();
        var storage = IntegrationEnvironment.Storage;
        var commandResult = storage.CreateCmd(
            "SELECT \"Id\", \"Name\", \"Deleted\" " +
            "FROM \"transformed_bool_records\" " +
            "WHERE \"Deleted\" = @deleted ORDER BY \"Id\";");
        Assert.That(commandResult.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(commandResult.Errors));
        Assert.That(commandResult.Result, Is.Not.Null);
        using var command = commandResult.Result!;

        var deletedParameter = storage.GetDbParameter();
        deletedParameter.ParameterName = "@deleted";
        deletedParameter.DbType = DbType.String;
        command.Parameters.Add(deletedParameter);

        var result = await storage.Query<TransformedBoolRecord>(command,
        [
            new Dictionary<string, object?> { ["@deleted"] = "N" },
            new Dictionary<string, object?> { ["@deleted"] = "Y" }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(result.Errors));
            Assert.That(result.Result, Has.Count.EqualTo(2));
            Assert.That(result.Result![0].Name, Is.EqualTo("Active"));
            Assert.That(result.Result[0].Deleted, Is.False,
                "SqlTransform.FromSql must convert N to false.");
            Assert.That(result.Result[1].Name, Is.EqualTo("Deleted"));
            Assert.That(result.Result[1].Deleted, Is.True,
                "SqlTransform.FromSql must convert Y to true.");
        });
    }

    [Test]
    public async Task Typed_command_query_reports_FromSql_failure_monadically()
    {
        var storage = IntegrationEnvironment.Storage;
        var commandResult = storage.CreateCmd(
            "SELECT 1 AS \"Id\", 'safe' AS \"NormalizedBeforeFailure\", " +
            "'TRIGGER' AS \"FailingValue\";");
        Assert.That(commandResult.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(commandResult.Errors));
        Assert.That(commandResult.Result, Is.Not.Null);
        using var command = commandResult.Result!;

        var result = await storage.Query<FailingBulkTransformRecord>(command, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Result, Is.Not.Null);
            Assert.That(result.Result, Is.Empty);
            Assert.That(IntegrationEnvironment.ErrorMessages(result.Errors),
                Does.Contain("intentional FromSql failure"));
        });
    }

    [Test]
    public async Task Typed_command_query_maps_Count_to_an_int_projection()
    {
        await Seed();
        var storage = IntegrationEnvironment.Storage;
        var commandResult = storage.CreateCmd(
            "SELECT COUNT(*) AS \"Count\" " +
            "FROM \"transformed_bool_records\" " +
            "WHERE \"Deleted\" = @deleted;");
        Assert.That(commandResult.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(commandResult.Errors));
        Assert.That(commandResult.Result, Is.Not.Null);
        using var command = commandResult.Result!;

        var deletedParameter = storage.GetDbParameter();
        deletedParameter.ParameterName = "@deleted";
        deletedParameter.DbType = DbType.String;
        command.Parameters.Add(deletedParameter);

        var result = await storage.Query<CountProjection>(command,
        [
            new Dictionary<string, object?> { ["@deleted"] = "N" },
            new Dictionary<string, object?> { ["@deleted"] = "Y" }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(result.Errors));
            Assert.That(result.Result, Has.Count.EqualTo(2));
            Assert.That(result.Result![0].Count, Is.EqualTo(1));
            Assert.That(result.Result[1].Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Negated_boolean_member_uses_the_transformed_false_value()
    {
        await Seed();

        var result = await TransformedBoolRecord.StartQuery()
            .Where(item => !item.Deleted)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name), Is.EqualTo(new[] { "Active" }));
    }

    [Test]
    public async Task Boolean_member_uses_the_transformed_true_value()
    {
        await Seed();

        var result = await TransformedBoolRecord.StartQuery()
            .Where(item => item.Deleted)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name), Is.EqualTo(new[] { "Deleted" }));
    }

    [TestCase(false, "Active")]
    [TestCase(true, "Deleted")]
    public async Task Captured_boolean_comparison_uses_the_field_transform(
        bool deleted,
        string expectedName)
    {
        await Seed();

        var result = await TransformedBoolRecord.StartQuery()
            .Where(item => item.Deleted == deleted)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name), Is.EqualTo(new[] { expectedName }));
    }

    [Test]
    public async Task Prepared_boolean_comparison_transforms_each_runtime_value()
    {
        await Seed();
        var deleted = false;
        var prepared = TransformedBoolRecord.StartQuery()
            .WhereWithParameters(item => item.Deleted == deleted);

        var active = await prepared.New()
            .Prepare(false)
            .RunWithError();
        var removed = await prepared.New()
            .Prepare(true)
            .RunWithError();

        Assert.Multiple(() =>
        {
            Assert.That(active.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(active.Errors));
            Assert.That(active.Result!.Select(item => item.Name),
                Is.EqualTo(new[] { "Active" }));
            Assert.That(removed.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(removed.Errors));
            Assert.That(removed.Result!.Select(item => item.Name),
                Is.EqualTo(new[] { "Deleted" }));
        });
    }

    [Test]
    public async Task Numeric_transform_changes_value_and_database_type()
    {
        var created = await TransformedNumberRecord.Create(
            new TransformedNumberRecord
            {
                Name = "Five",
                Number = 5,
                OtherNumber = 20
            });

        var raw = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Number\", \"OtherNumber\" " +
            "FROM \"transformed_number_records\" " +
            $"WHERE \"Id\" = {created!.Id};");
        var loaded = await ((TransformedNumberRecordManager)
                GenericDM.Get<TransformedNumberRecord>())
            .GetByIdWithErrorNoCache<TransformedNumberRecord>(created.Id);

        Assert.Multiple(() =>
        {
            Assert.That(raw.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(raw.Errors));
            Assert.That(raw.Result![0]["Number"], Is.EqualTo("10005"));
            Assert.That(raw.Result[0]["OtherNumber"], Is.EqualTo("10020"));
            Assert.That(loaded.Result!.Number, Is.EqualTo(5));
            Assert.That(loaded.Result.OtherNumber, Is.EqualTo(20));
        });
    }

    [TestCase("equal")]
    [TestCase("not-equal")]
    [TestCase("less")]
    [TestCase("less-or-equal")]
    [TestCase("greater")]
    [TestCase("greater-or-equal")]
    public async Task Numeric_transform_is_applied_to_comparison_operators(
        string comparison)
    {
        await SeedNumbers();

        var query = TransformedNumberRecord.StartQuery();
        switch (comparison)
        {
            case "equal":
                query.Where(item => item.Number == 5);
                break;
            case "not-equal":
                query.Where(item => item.Number != 5);
                break;
            case "less":
                query.Where(item => item.Number < 5);
                break;
            case "less-or-equal":
                query.Where(item => item.Number <= 5);
                break;
            case "greater":
                query.Where(item => item.Number > 5);
                break;
            case "greater-or-equal":
                query.Where(item => item.Number >= 5);
                break;
        }

        var result = await query.RunWithError();

        string[] expected = comparison switch
        {
            "equal" => ["Exact"],
            "not-equal" => ["Low", "High"],
            "less" => ["Low"],
            "less-or-equal" => ["Low", "Exact"],
            "greater" => ["High"],
            _ => ["Exact", "High"]
        };
        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EquivalentTo(expected));
    }

    [Test]
    public async Task Numeric_transform_supports_value_on_the_left()
    {
        await SeedNumbers();
        var expected = 5;

        var result = await TransformedNumberRecord.StartQuery()
            .Where(item => expected == item.Number)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Exact" }));
    }

    [Test]
    public async Task Numeric_transform_reverses_ordered_comparison_with_field_on_right()
    {
        await SeedNumbers();
        var minimum = 5;

        var result = await TransformedNumberRecord.StartQuery()
            .Where(item => minimum < item.Number)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "High" }));
    }

    [Test]
    public async Task Numeric_transform_is_applied_to_each_Contains_value()
    {
        await SeedNumbers();
        var accepted = new List<int> { 2, 9 };

        var result = await TransformedNumberRecord.StartQuery()
            .Where(item => accepted.Contains(item.Number))
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EquivalentTo(new[] { "Low", "High" }));
    }

    [Test]
    public async Task Prepared_numeric_transform_supports_SetVariables()
    {
        await SeedNumbers();
        var expected = 0;
        var prepared = TransformedNumberRecord.StartQuery()
            .WhereWithParameters(item => item.Number == expected);

        var result = await prepared.New()
            .SetVariables(set => set(nameof(expected), 9))
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "High" }));
    }

    [Test]
    public async Task Different_transformed_fields_keep_their_own_query_values()
    {
        await SeedNumbers();

        var result = await TransformedNumberRecord.StartQuery()
            .Where(item => item.Number == 5 && item.OtherNumber == 20)
            .RunWithError();

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Exact" }));
    }

    [Test]
    public async Task ToSql_exception_is_reported_when_the_query_runs()
    {
        var creation = await ThrowingQueryTransformRecord.CreateWithError(
            new ThrowingQueryTransformRecord { Name = "Safe", Number = 1 });
        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));

        var query = ThrowingQueryTransformRecord.StartQuery()
            .Where(item => item.Number == 13);
        var result = await query.RunWithError();

        Assert.That(result.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(result.Errors),
            Does.Contain("Query transform rejected 13"));
    }

    private static async Task Seed()
    {
        var creation = await TransformedBoolRecord.CreateWithError(
        [
            new TransformedBoolRecord { Name = "Active", Deleted = false },
            new TransformedBoolRecord { Name = "Deleted", Deleted = true }
        ]);
        Assert.That(creation.Success, Is.True, IntegrationEnvironment.ErrorMessages(creation.Errors));
    }

    private static async Task SeedNumbers()
    {
        var creation = await TransformedNumberRecord.CreateWithError(
        [
            new TransformedNumberRecord
                { Name = "Low", Number = 2, OtherNumber = 10 },
            new TransformedNumberRecord
                { Name = "Exact", Number = 5, OtherNumber = 20 },
            new TransformedNumberRecord
                { Name = "High", Number = 9, OtherNumber = 30 }
        ]);
        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
    }
}

public sealed class CountProjection
{
    public int Count { get; set; }
}
