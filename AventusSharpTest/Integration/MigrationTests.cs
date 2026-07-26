using AventusSharp.Data.Attributes;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Sqlite;
using AventusSharp.Tools;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class MigrationTests
{
    private SqliteMigrationProvider _provider = null!;

    [SetUp]
    public async Task SetUp()
    {
        _provider = new SqliteMigrationProvider(IntegrationEnvironment.Storage);
        var result = new VoidWithError();
        await result.RunAsync(_provider.Init);
        await result.RunAsync(() => IntegrationEnvironment.Storage.Execute(
            "DROP TABLE IF EXISTS \"migration_test_entities\";"));

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task CreateModel_creates_the_declared_table_and_columns()
    {
        var result = await new CreateEntityMigration()._Up([_provider]);
        var columns = await IntegrationEnvironment.Storage.Query(
            "PRAGMA table_info(\"migration_test_entities\");");

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(columns.Success, Is.True, IntegrationEnvironment.ErrorMessages(columns.Errors));
        Assert.That(columns.Result!.Select(column => column["name"]),
            Is.EquivalentTo(new[] { "Id", "Name", "Quantity" }));
    }

    [Test]
    [Explicit("Specification: property rename/update is not implemented yet.")]
    public async Task RenameProperty_preserves_data_and_exposes_the_new_column()
    {
        var result = new VoidWithError();
        await result.RunAsync(() => new CreateEntityMigration()._Up([_provider]));
        await result.RunAsync(() => IntegrationEnvironment.Storage.Execute(
            "INSERT INTO \"migration_test_entities\" (\"Name\", \"Quantity\") VALUES ('before', 4);"));
        await result.RunAsync(() => new RenameEntityPropertyMigration()._Up([_provider]));

        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Label\", \"Quantity\" FROM \"migration_test_entities\";");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["Label"], Is.EqualTo("before"));
    }

    [Test]
    [Explicit("Specification: model deletion is not implemented yet.")]
    public async Task DeleteModel_removes_the_table()
    {
        var result = new VoidWithError();
        await result.RunAsync(() => new CreateEntityMigration()._Up([_provider]));
        await result.RunAsync(() => new DeleteEntityMigration()._Up([_provider]));
        var exists = await IntegrationEnvironment.Storage.Query(
            "SELECT COUNT(*) AS count FROM sqlite_master " +
            "WHERE type = 'table' AND name = 'migration_test_entities';");

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(exists.Result!.Single()["count"], Is.EqualTo("0"));
    }
}

[ManualInit]
internal sealed class CreateEntityMigration : Migration
{
    public override string GetName() => $"test_create_entity_{Guid.NewGuid():N}";

    public override void Up()
    {
        CreateModel<MigrationTestEntity>()
            .AddPrimary("Id")
            .AddProperty<string>("Name", new() { Size = new Size(100) })
            .AddProperty<int>("Quantity", new() { Default = 0 });
    }

    public override void Down()
    {
        DeleteModel<MigrationTestEntity>();
    }
}

[ManualInit]
internal sealed class RenameEntityPropertyMigration : Migration
{
    public override string GetName() => $"test_rename_entity_{Guid.NewGuid():N}";

    public override void Up()
    {
        SelectModel<MigrationTestEntity>().RenameProperty<string>("Name", "Label");
    }

    public override void Down()
    {
        SelectModel<MigrationTestEntity>().RenameProperty<string>("Label", "Name");
    }
}

[ManualInit]
internal sealed class DeleteEntityMigration : Migration
{
    public override string GetName() => $"test_delete_entity_{Guid.NewGuid():N}";

    public override void Up()
    {
        DeleteModel<MigrationTestEntity>();
    }

    public override void Down()
    {
    }
}
