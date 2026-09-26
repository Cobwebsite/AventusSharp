using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default;
using AventusSharpTest.Integration.Containers;
using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class MigrationDeletionStorageTests
{
    private const string Parent = "migration_delete_parent";
    private const string Child = "migration_delete_child";
    private const string Peer = "migration_delete_peer";
    private const string OutgoingLink = Parent + "_" + Peer;
    private const string IncomingLink = Peer + "_" + Parent;

    private static IDBStorage Storage(string kind)
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

    private static async Task Execute(IDBStorage storage, string sql)
    {
        var result = await storage.Execute(sql);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    private static async Task Prepare(IDBStorage storage, bool dependent = false)
    {
        foreach (Type type in new[] { typeof(DeletionParent), typeof(DeletionPeer), typeof(DeletionChild) })
        {
            var registered = storage.AddPyramid(new(type, new()));
            Assert.That(registered.Success, Is.True, IntegrationEnvironment.ErrorMessages(registered.Errors));
        }
        string Q(string name) => storage.QuoteIdentifier(name);
        foreach (string table in new[] { IncomingLink, OutgoingLink, Child, Parent, Peer })
            await Execute(storage, $"DROP TABLE IF EXISTS {Q(table)}");
        await Execute(storage, $"CREATE TABLE {Q(Parent)} ({Q("Id")} int PRIMARY KEY, {Q("Name")} varchar(100) UNIQUE)");
        await Execute(storage, $"CREATE INDEX {Q("migration_delete_index")} ON {Q(Parent)} ({Q("Name")})");
        await Execute(storage, $"CREATE TABLE {Q(Peer)} ({Q("Id")} int PRIMARY KEY)");
        await Execute(storage, $"INSERT INTO {Q(Parent)} VALUES (1, 'before')");
        await Execute(storage, $"INSERT INTO {Q(Peer)} VALUES (1)");
        foreach (string link in new[] { IncomingLink, OutgoingLink })
        {
            await Execute(storage, $"CREATE TABLE {Q(link)} ({Q("ParentId")} int, {Q("PeerId")} int, "
                + $"PRIMARY KEY ({Q("ParentId")}, {Q("PeerId")}), "
                + $"FOREIGN KEY ({Q("ParentId")}) REFERENCES {Q(Parent)} ({Q("Id")}), "
                + $"FOREIGN KEY ({Q("PeerId")}) REFERENCES {Q(Peer)} ({Q("Id")}))");
            await Execute(storage, $"INSERT INTO {Q(link)} VALUES (1, 1)");
        }
        if (dependent)
        {
            await Execute(storage, $"CREATE TABLE {Q(Child)} ({Q("Id")} int PRIMARY KEY, {Q("ParentId")} int, "
                + $"FOREIGN KEY ({Q("ParentId")}) REFERENCES {Q(Parent)} ({Q("Id")}))");
            await Execute(storage, $"INSERT INTO {Q(Child)} VALUES (1, 1)");
        }
    }

    private static async Task<VoidWithError> Delete(IDBStorage storage, params IMigrationModel[] models)
    {
        var provider = storage.GetMigrationProvider();
        var transactions = (IStorageMigrationProvider)provider;
        var transaction = await transactions.BeginTransaction();
        VoidWithError result = new() { Errors = transaction.Errors };
        if (!result.Success || transaction.Result == null) return result;
        transactions.setTransactionScope(transaction.Result);
        try
        {
            await result.RunAsync(() => transactions.DeleteModels(models));
            await provider.AfterUp(result);
            return result;
        }
        finally
        {
            transactions.setTransactionScope(null);
        }
    }

    private static IMigrationModel Model<T>() where T : IStorable
    {
        var migration = new DeletionBatchMigration();
        var model = migration.SelectModel<T>();
        migration.DeleteModel<T>();
        return model;
    }

    private static async Task AssertExists(IDBStorage storage, string table, bool expected)
    {
        var exists = await storage.TableExist(table);
        Assert.That(exists.Success, Is.True, IntegrationEnvironment.ErrorMessages(exists.Errors));
        Assert.That(exists.Result, Is.EqualTo(expected), table);
    }

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Delete_removes_both_intermediate_tables_and_preserves_peer(string kind)
    {
        var storage = Storage(kind);
        await Prepare(storage);
        var result = await Delete(storage, Model<DeletionParent>());
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        foreach (string table in new[] { Parent, IncomingLink, OutgoingLink })
            await AssertExists(storage, table, false);
        var peers = await storage.Query($"SELECT * FROM {storage.QuoteIdentifier(Peer)}");
        Assert.That(peers.Success, Is.True, IntegrationEnvironment.ErrorMessages(peers.Errors));
        Assert.That(peers.Result!.Single()["Id"], Is.EqualTo("1"));
        // Reusing the index name proves that the deleted table's index was removed.
        await Execute(storage, $"CREATE INDEX {storage.QuoteIdentifier("migration_delete_index")} ON {storage.QuoteIdentifier(Peer)} ({storage.QuoteIdentifier("Id")})");
        result = await Delete(storage, Model<DeletionParent>());
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task External_dependency_blocks_deletion_before_any_table_is_removed(string kind)
    {
        var storage = Storage(kind);
        await Prepare(storage, dependent: true);
        var result = await Delete(storage, Model<DeletionParent>(), Model<DeletionPeer>());
        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.OfType<DataError>().Any(error => error.Code == DataErrorCode.ModelDeletionBlocked), Is.True);
        foreach (string table in new[] { Parent, Peer, Child, IncomingLink, OutgoingLink })
            await AssertExists(storage, table, true);
        await Execute(storage, $"INSERT INTO {storage.QuoteIdentifier(Child)} VALUES (2, 1)");
        var invalid = await storage.Execute($"INSERT INTO {storage.QuoteIdentifier(Child)} VALUES (3, 999)");
        Assert.That(invalid.Success, Is.False, "The retained foreign key must still be enforced.");
    }

    [TestCase("SQLite")]
    [TestCase("MySQL")]
    [TestCase("PostgreSQL")]
    [TestCase("SQLServer")]
    public async Task Batch_deletion_handles_parent_first_and_foreign_key_cycles(string kind)
    {
        var storage = Storage(kind);
        await Prepare(storage, dependent: true);
        if (kind == "SQLite")
            await Execute(storage, $"ALTER TABLE {storage.QuoteIdentifier(Parent)} ADD {storage.QuoteIdentifier("ChildId")} int "
                + $"REFERENCES {storage.QuoteIdentifier(Child)} ({storage.QuoteIdentifier("Id")})");
        else
        {
            await Execute(storage, $"ALTER TABLE {storage.QuoteIdentifier(Parent)} ADD {storage.QuoteIdentifier("ChildId")} int");
            await Execute(storage, $"ALTER TABLE {storage.QuoteIdentifier(Parent)} ADD CONSTRAINT {storage.QuoteIdentifier("migration_delete_cycle")} "
                + $"FOREIGN KEY ({storage.QuoteIdentifier("ChildId")}) REFERENCES {storage.QuoteIdentifier(Child)} ({storage.QuoteIdentifier("Id")})");
        }
        await Execute(storage, $"UPDATE {storage.QuoteIdentifier(Parent)} SET {storage.QuoteIdentifier("ChildId")} = 1");
        var result = await Delete(storage, Model<DeletionParent>(), Model<DeletionChild>());
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        foreach (string table in new[] { Parent, Child, IncomingLink, OutgoingLink })
            await AssertExists(storage, table, false);
        await AssertExists(storage, Peer, true);
    }

    [Test]
    public async Task Migration_up_batches_model_deletions_instead_of_following_declaration_order()
    {
        var storage = Storage("SQLite");
        await Prepare(storage, dependent: true);
        var provider = storage.GetMigrationProvider();
        var initialized = await provider.Init();
        Assert.That(initialized.Success, Is.True, IntegrationEnvironment.ErrorMessages(initialized.Errors));
        var result = await new DeletionBatchMigration()._Up([provider]);
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        await AssertExists(storage, Parent, false);
        await AssertExists(storage, Child, false);
    }
}

[ManualInit]
[SqlName("migration_delete_parent")]
public sealed class DeletionParent : Storable<DeletionParent>
{
    [ForeignKey<DeletionPeer>]
    public List<int> PeerIds { get; set; } = [];
}

[ManualInit]
[SqlName("migration_delete_peer")]
public sealed class DeletionPeer : Storable<DeletionPeer>
{
    [ForeignKey<DeletionParent>]
    public List<int> ParentIds { get; set; } = [];
}

[ManualInit]
[SqlName("migration_delete_child")]
public sealed class DeletionChild : Storable<DeletionChild>
{
    [ForeignKey<DeletionParent>]
    public int ParentId { get; set; }
}

[ManualInit]
internal sealed class DeletionBatchMigration : Migration
{
    public override string GetName() => "test_delete_batch_" + Guid.NewGuid().ToString("N");
    public override void Up()
    {
        DeleteModel<DeletionParent>();
        DeleteModel<DeletionChild>();
    }
    public override void Down() { }
}
