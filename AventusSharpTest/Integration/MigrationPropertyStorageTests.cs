using AventusSharp.Data.Attributes;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default;
using AventusSharpTest.Integration.Containers;
using AventusSharpTest.Integration.Models;
using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class MigrationPropertyStorageTests
{
    private static IDBStorage GetStorage(string kind)
    {
        if (kind != "SQLite") DatabaseContainers.RequireDocker();
        return kind switch
        {
            "MySQL" => DatabaseContainers.MySql,
            "PostgreSQL" => DatabaseContainers.PostgreSql,
            "SQLServer" => DatabaseContainers.MsSql,
            _ => IntegrationEnvironment.Storage
        };
    }

    private static async Task<VoidWithError> Apply(IDBStorage storage, MigrationModel<MigrationTestEntity> model)
    {
        var provider = storage.GetMigrationProvider();
        var transactions = (IStorageMigrationProvider)provider;
        var transaction = await transactions.BeginTransaction();
        VoidWithError result = new() { Errors = transaction.Errors };
        if (!result.Success || transaction.Result == null) return result;
        transactions.setTransactionScope(transaction.Result);
        try
        {
            await result.RunAsync(() => provider.ApplyMigration<MigrationTestEntity>(model));
            await provider.AfterUp(result);
            return result;
        }
        finally
        {
            transactions.setTransactionScope(null);
        }
    }

    private static async Task Prepare(IDBStorage storage)
    {
        string Q(string name) => storage.QuoteIdentifier(name);
        foreach (string sql in new[]
        {
            $"DROP TABLE IF EXISTS {Q("migration_test_entities")}",
            $"DROP TABLE IF EXISTS {Q("migration_parent")}",
            $"CREATE TABLE {Q("migration_parent")} ({Q("Id")} int PRIMARY KEY)",
            $"INSERT INTO {Q("migration_parent")} ({Q("Id")}) VALUES (1)",
            $"CREATE TABLE {Q("migration_test_entities")} ({Q("Id")} int PRIMARY KEY, {Q("Name")} varchar(100) NOT NULL UNIQUE, {Q("Quantity")} int NOT NULL DEFAULT 0, {Q("ParentId")} int, FOREIGN KEY ({Q("ParentId")}) REFERENCES {Q("migration_parent")} ({Q("Id")}))",
            $"CREATE INDEX {Q("migration_quantity_index")} ON {Q("migration_test_entities")} ({Q("Quantity")})",
            $"INSERT INTO {Q("migration_test_entities")} ({Q("Id")}, {Q("Name")}, {Q("Quantity")}, {Q("ParentId")}) VALUES (1, 'before', 4, 1)"
        })
        {
            var executed = await storage.Execute(sql);
            Assert.That(executed.Success, Is.True, IntegrationEnvironment.ErrorMessages(executed.Errors));
        }
    }

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Rename_round_trip_preserves_rows_indexes_and_foreign_keys(string kind)
    {
        var storage = GetStorage(kind);
        await Prepare(storage);
        var rename = new MigrationModel<MigrationTestEntity>();
        rename.RenameProperty("Name", "Label");
        var result = await Apply(storage, rename);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        var rows = await storage.Query($"SELECT {storage.QuoteIdentifier("Label")} FROM {storage.QuoteIdentifier("migration_test_entities")}");
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single()["Label"], Is.EqualTo("before"));

        var restore = new MigrationModel<MigrationTestEntity>();
        restore.RenameProperty("Label", "Name");
        result = await Apply(storage, restore);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        await AssertConstraints(storage);
    }

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Update_changes_type_nullability_size_and_default_without_losing_rows(string kind)
    {
        var storage = GetStorage(kind);
        await Prepare(storage);
        var update = new MigrationModel<MigrationTestEntity>();
        update.UpdateProperty<string>("Name", new() { Size = new Size(200), Nullable = true, Default = "new default" });
        update.UpdateProperty<long>("Quantity", new() { Default = 9 });
        var result = await Apply(storage, update);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        string Q(string name) => storage.QuoteIdentifier(name);
        var insertion = await storage.Execute($"INSERT INTO {Q("migration_test_entities")} ({Q("Id")}) VALUES (2)");
        Assert.That(insertion.Success, Is.True, IntegrationEnvironment.ErrorMessages(insertion.Errors));
        var rows = await storage.Query($"SELECT {Q("Name")}, {Q("Quantity")} FROM {Q("migration_test_entities")} ORDER BY {Q("Id")}");
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(row => row["Name"]), Is.EqualTo(new[] { "before", "new default" }));
        Assert.That(rows.Result!.Select(row => row["Quantity"]), Is.EqualTo(new[] { "4", "9" }));
        var nullable = await storage.Execute($"INSERT INTO {Q("migration_test_entities")} ({Q("Id")}, {Q("Name")}) VALUES (3, NULL)");
        Assert.That(nullable.Success, Is.True, IntegrationEnvironment.ErrorMessages(nullable.Errors));
        await AssertConstraints(storage);
    }

    [Test]
    public async Task SqlServer_update_preserves_index_keys_includes_filter_and_options()
    {
        var storage = GetStorage("SQLServer");
        await Prepare(storage);
        var created = await storage.Execute("CREATE UNIQUE INDEX [migration_composite_index] "
            + "ON [migration_test_entities] ([Quantity] DESC, [Id] ASC) INCLUDE ([ParentId]) "
            + "WHERE [Quantity] > 0 WITH (FILLFACTOR = 80, PAD_INDEX = ON, ALLOW_PAGE_LOCKS = OFF)");
        Assert.That(created.Success, Is.True, IntegrationEnvironment.ErrorMessages(created.Errors));
        string metadata = "SELECT i.name, i.is_unique, i.filter_definition, i.fill_factor, i.is_padded, i.allow_page_locks, "
            + "c.name AS column_name, ic.key_ordinal, ic.is_descending_key, ic.is_included_column "
            + "FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id "
            + "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id "
            + "WHERE i.object_id = OBJECT_ID('[migration_test_entities]') AND i.name LIKE 'migration_%index' "
            + "ORDER BY i.name, ic.index_column_id";
        var before = await storage.Query(metadata);
        Assert.That(before.Success, Is.True, IntegrationEnvironment.ErrorMessages(before.Errors));
        var update = new MigrationModel<MigrationTestEntity>();
        update.UpdateProperty<long>("Quantity", new() { Default = 9 });
        var result = await Apply(storage, update);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        var after = await storage.Query(metadata);
        Assert.That(after.Success, Is.True, IntegrationEnvironment.ErrorMessages(after.Errors));
        Assert.That(after.Result, Is.EqualTo(before.Result));
    }

    [Test]
    public async Task SqlServer_failed_update_returns_original_error_and_restores_schema_and_indexes()
    {
        var storage = GetStorage("SQLServer");
        await Prepare(storage);
        var update = new MigrationModel<MigrationTestEntity>();
        update.UpdateProperty<long>("Quantity", new() { Default = 9 });
        update.UpdateProperty<string>("Name", new() { Size = new Size(1) });
        var result = await Apply(storage, update);
        Assert.That(result.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(result.Errors), Does.Not.Contain("SqlTransaction"));
        var columns = await storage.Query("SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH "
            + "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'migration_test_entities'");
        Assert.That(columns.Success, Is.True, IntegrationEnvironment.ErrorMessages(columns.Errors));
        Assert.That(columns.Result!.Single(column => column["COLUMN_NAME"] == "Quantity")["DATA_TYPE"], Is.EqualTo("int"));
        Assert.That(columns.Result!.Single(column => column["COLUMN_NAME"] == "Name")["CHARACTER_MAXIMUM_LENGTH"], Is.EqualTo("100"));
        var indexes = await storage.Query("SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('[migration_test_entities]') "
            + "AND name = 'migration_quantity_index'");
        Assert.That(indexes.Success, Is.True, IntegrationEnvironment.ErrorMessages(indexes.Errors));
        Assert.That(indexes.Result, Has.Count.EqualTo(1));
        var insertion = await storage.Execute("INSERT INTO [migration_test_entities] ([Id], [Name]) VALUES (2, 'after')");
        Assert.That(insertion.Success, Is.True, IntegrationEnvironment.ErrorMessages(insertion.Errors));
        var rows = await storage.Query("SELECT [Name], [Quantity] FROM [migration_test_entities] ORDER BY [Id]");
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(row => row["Name"]), Is.EqualTo(new[] { "before", "after" }));
        Assert.That(rows.Result!.Select(row => row["Quantity"]), Is.EqualTo(new[] { "4", "0" }));
    }

    private static async Task AssertConstraints(IDBStorage storage)
    {
        string Q(string name) => storage.QuoteIdentifier(name);
        var duplicate = await storage.Execute($"INSERT INTO {Q("migration_test_entities")} ({Q("Id")}, {Q("Name")}) VALUES (10, 'before')");
        Assert.That(duplicate.Success, Is.False, "The unique constraint must be preserved.");
        var invalidLink = await storage.Execute($"INSERT INTO {Q("migration_test_entities")} ({Q("Id")}, {Q("Name")}, {Q("ParentId")}) VALUES (11, 'invalid link', 999)");
        Assert.That(invalidLink.Success, Is.False, "The foreign key must be preserved.");
    }
}
