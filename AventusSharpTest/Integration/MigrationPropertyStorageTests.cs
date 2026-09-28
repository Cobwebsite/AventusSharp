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

    private static async Task<VoidWithError> Apply(IDBStorage storage, IMigrationModel model)
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

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Create_and_delete_columns_preserve_rows_and_unrelated_constraints(string kind)
    {
        var storage = GetStorage(kind);
        await Prepare(storage);
        var add = new MigrationModel<MigrationTestEntity>();
        add.AddProperty<int>("Added", new() { Default = 7, Index = true });
        add.AddProperty<string>("UniqueAdded", new() { Nullable = true, Unique = true, Size = new Size(80) });
        var result = await Apply(storage, add);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        string Q(string name) => storage.QuoteIdentifier(name);
        var rows = await storage.Query($"SELECT {Q("Added")}, {Q("Name")}, {Q("Quantity")} FROM {Q("migration_test_entities")}");
        Assert.That(rows.Result!.Single()["Added"], Is.EqualTo("7"));
        Assert.That(rows.Result!.Single()["Name"], Is.EqualTo("before"));
        Assert.That(rows.Result!.Single()["Quantity"], Is.EqualTo("4"));
        await AssertConstraints(storage);
        result = await Apply(storage, add);
        Assert.That(result.Success, Is.False, "Duplicate creation must not silently ignore new options.");
        var remove = new MigrationModel<MigrationTestEntity>();
        remove.RemoveProperty("Added");
        remove.RemoveProperty("UniqueAdded");
        remove.RemoveProperty("ParentId");
        result = await Apply(storage, remove);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        rows = await storage.Query($"SELECT * FROM {Q("migration_test_entities")}");
        Assert.That(rows.Result!.Single().Keys, Is.EquivalentTo(new[] { "Id", "Name", "Quantity" }));
        Assert.That(rows.Result!.Single()["Name"], Is.EqualTo("before"));
        result = await Apply(storage, remove);
        Assert.That(result.Success, Is.True, "Deleting an absent column is idempotent.");
        var duplicate = await storage.Execute($"INSERT INTO {Q("migration_test_entities")} ({Q("Id")}, {Q("Name")}) VALUES (2, 'before')");
        Assert.That(duplicate.Success, Is.False);
    }

    [TestCase("SQLite")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Failed_creation_rolls_back_prior_column_changes(string kind)
    {
        var storage = GetStorage(kind);
        await Prepare(storage);
        var add = new MigrationModel<MigrationTestEntity>();
        add.AddProperty<int>("Added", new() { Default = 7 });
        add.AddProperty<int>("Impossible", new() { Unique = true });
        var result = await Apply(storage, add);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Is.Not.Empty);
        var rows = await storage.Query($"SELECT * FROM {storage.QuoteIdentifier("migration_test_entities")}");
        Assert.That(rows.Result!.Single().Keys, Is.EquivalentTo(new[] { "Id", "Name", "Quantity", "ParentId" }));
        await AssertConstraints(storage);
    }

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Create_reference_enforces_foreign_key_and_delete_set_null(string kind)
    {
        var storage = GetStorage(kind);
        await Prepare(storage);
        string Q(string name) => storage.QuoteIdentifier(name);
        await storage.Execute($"DROP TABLE IF EXISTS {Q("migration_column_peer")}");
        var setup = await storage.Execute($"CREATE TABLE {Q("migration_column_peer")} ({Q("Id")} int PRIMARY KEY)");
        Assert.That(setup.Success, Is.True, IntegrationEnvironment.ErrorMessages(setup.Errors));
        await storage.Execute($"INSERT INTO {Q("migration_column_peer")} VALUES (1)");
        var add = new MigrationModel<MigrationTestEntity>();
        add.AddRef<MigrationColumnPeer>("NewParent", new() { Nullable = true, DeleteKind = DeleteKind.DeleteSetNull });
        var result = await Apply(storage, add);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        var invalid = await storage.Execute($"UPDATE {Q("migration_test_entities")} SET {Q("NewParent")} = 999");
        Assert.That(invalid.Success, Is.False);
        var valid = await storage.Execute($"UPDATE {Q("migration_test_entities")} SET {Q("NewParent")} = 1");
        Assert.That(valid.Success, Is.True, IntegrationEnvironment.ErrorMessages(valid.Errors));
        await storage.Execute($"DELETE FROM {Q("migration_column_peer")}");
        var rows = await storage.Query($"SELECT {Q("NewParent")} FROM {Q("migration_test_entities")}");
        Assert.That(rows.Result!.Single()["NewParent"], Is.Null);
        var remove = new MigrationModel<MigrationTestEntity>();
        remove.RemoveProperty("NewParent");
        result = await Apply(storage, remove);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        await AssertConstraints(storage);
    }

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Collection_creation_and_deletion_manage_intermediate_table(string kind)
    {
        var storage = GetStorage(kind);
        await Prepare(storage);
        string Q(string name) => storage.QuoteIdentifier(name);
        const string linkTable = "migration_test_entities_migration_column_peer";
        await storage.Execute($"DROP TABLE IF EXISTS {Q(linkTable)}");
        await storage.Execute($"DROP TABLE IF EXISTS {Q("migration_column_peer")}");
        var setup = await storage.Execute($"CREATE TABLE {Q("migration_column_peer")} ({Q("Id")} int PRIMARY KEY)");
        Assert.That(setup.Success, Is.True, IntegrationEnvironment.ErrorMessages(setup.Errors));
        await storage.Execute($"INSERT INTO {Q("migration_column_peer")} VALUES (1)");
        var add = new MigrationModel<MigrationTestEntity>();
        add.AddProperty<List<MigrationColumnPeer>>("Peers");
        var result = await Apply(storage, add);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        var insertion = await storage.Execute($"INSERT INTO {Q(linkTable)} VALUES (1, 1)");
        Assert.That(insertion.Success, Is.True, IntegrationEnvironment.ErrorMessages(insertion.Errors));
        insertion = await storage.Execute($"INSERT INTO {Q(linkTable)} VALUES (1, 999)");
        Assert.That(insertion.Success, Is.False, "The linked table must have a foreign key.");
        var remove = new MigrationModel<MigrationTestEntity>();
        remove.RemoveProperty<List<MigrationColumnPeer>>("Peers");
        result = await Apply(storage, remove);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        var exists = await storage.TableExist(linkTable);
        Assert.That(exists.Result, Is.False);
        await AssertConstraints(storage);
        var peers = await storage.Query($"SELECT * FROM {Q("migration_column_peer")}");
        Assert.That(peers.Result, Has.Count.EqualTo(1));
    }

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Incoming_foreign_key_blocks_column_deletion_without_losing_constraints(string kind)
    {
        var storage = GetStorage(kind);
        string Q(string name) => storage.QuoteIdentifier(name);
        await storage.Execute($"DROP TABLE IF EXISTS {Q("migration_column_dependent")}");
        await Prepare(storage);
        var created = await storage.Execute($"CREATE TABLE {Q("migration_column_dependent")} ({Q("EntityId")} int REFERENCES {Q("migration_test_entities")} ({Q("Id")}))");
        // MySQL requires a table-level constraint to enforce the reference.
        if (kind == "MySQL")
            created = await storage.Execute($"ALTER TABLE {Q("migration_column_dependent")} ADD FOREIGN KEY ({Q("EntityId")}) REFERENCES {Q("migration_test_entities")} ({Q("Id")})");
        Assert.That(created.Success, Is.True, IntegrationEnvironment.ErrorMessages(created.Errors));
        try
        {
            var remove = new MigrationModel<MigrationTestEntity>();
            remove.RemoveProperty("Id");
            var result = await Apply(storage, remove);
            Assert.That(result.Success, Is.False);
            await AssertConstraints(storage);
            var rows = await storage.Query($"SELECT {Q("Id")} FROM {Q("migration_test_entities")}");
            Assert.That(rows.Result!.Single()["Id"], Is.EqualTo("1"));
        }
        finally
        {
            await storage.Execute($"DROP TABLE {Q("migration_column_dependent")}");
        }
    }

    [Test]
    public async Task Sqlite_rebuild_preserves_autoincrement_high_water_mark_and_triggers()
    {
        var storage = GetStorage("SQLite");
        await Prepare(storage);
        await storage.Execute("DROP TABLE migration_test_entities");
        await storage.Execute("CREATE TABLE migration_test_entities (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, Quantity int)");
        await storage.Execute("INSERT INTO migration_test_entities VALUES (100, 'deleted', 0)");
        await storage.Execute("DELETE FROM migration_test_entities");
        await storage.Execute("INSERT INTO migration_test_entities VALUES (1, 'before', 4)");
        await storage.Execute("CREATE TRIGGER migration_column_trigger AFTER INSERT ON migration_test_entities BEGIN UPDATE migration_test_entities SET Quantity = 42 WHERE Id = NEW.Id; END");
        var add = new MigrationModel<MigrationTestEntity>();
        add.AddProperty<string>("Added", new() { Nullable = true });
        var result = await Apply(storage, add);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        var remove = new MigrationModel<MigrationTestEntity>();
        remove.RemoveProperty("Added");
        result = await Apply(storage, remove);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        var inserted = await storage.Execute("INSERT INTO migration_test_entities (Name) VALUES ('after')");
        Assert.That(inserted.Success, Is.True, IntegrationEnvironment.ErrorMessages(inserted.Errors));
        var rows = await storage.Query("SELECT Id, Quantity FROM migration_test_entities WHERE Name = 'after'");
        Assert.That(rows.Result!.Single()["Id"], Is.EqualTo("101"));
        Assert.That(rows.Result!.Single()["Quantity"], Is.EqualTo("42"));
    }
}

[ManualInit]
[SqlName("migration_column_peer")]
public sealed class MigrationColumnPeer : AventusSharp.Data.Storable<MigrationColumnPeer> { }
