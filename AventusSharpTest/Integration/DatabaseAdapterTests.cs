using System.Data;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Mssql;
using AventusSharp.Data.Storage.Mysql;
using AventusSharp.Data.Storage.Postgresql;
using AventusSharp.Data.Storage.Sqlite;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Attributes;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
public sealed class DatabaseAdapterTests
{
    public static IEnumerable<TestCaseData> Adapters()
    {
        var credentials = new StorageCredentials()
        {
            Host = "localhost",
            Username = "user",
            Password = "password",
            Database = "database"
        };
        yield return new TestCaseData(new SqliteStorage("adapter-contract.db", false), "sqlite");
        yield return new TestCaseData(new MySQLStorage(credentials, false), "mysql");
        yield return new TestCaseData(new PostgreSqlStorage(credentials, false), "postgresql");
        yield return new TestCaseData(new MsSqlStorage(credentials, false), "mssql");
    }

    [TestCaseSource(nameof(Adapters))]
    public void Adapter_exposes_expected_diagram_type(object storage, string expected)
    {
        Assert.That(((dynamic)storage).DiagramType(), Is.EqualTo(expected));
    }

    [Test]
    public void Every_adapter_maps_core_database_types()
    {
        var credentials = new StorageCredentials()
        {
            Host = "localhost",
            Username = "user",
            Password = "password",
            Database = "database"
        };
        var adapters = new (object Storage, string Name)[]
        {
            (new SqliteStorage("adapter-types.db", false), "sqlite"),
            (new MySQLStorage(credentials, false), "mysql"),
            (new PostgreSqlStorage(credentials, false), "postgresql"),
            (new MsSqlStorage(credentials, false), "mssql")
        };

        foreach (var (storage, name) in adapters)
        {
            dynamic adapter = storage;
            Assert.Multiple(() =>
            {
                Assert.That(adapter.GetSqlColumnType(DbType.Int32, null), Is.Not.Empty, name);
                Assert.That(adapter.GetSqlColumnType(DbType.Int64, null), Is.Not.Empty, name);
                Assert.That(adapter.GetSqlColumnType(DbType.String, null), Is.Not.Empty, name);
                Assert.That(adapter.GetSqlColumnType(DbType.Boolean, null), Is.Not.Empty, name);
                Assert.That(adapter.GetSqlColumnType(DbType.Date, null), Is.Not.Empty, name);
                Assert.That(adapter.GetSqlColumnType(DbType.DateTime, null), Is.Not.Empty, name);
                Assert.That(adapter.GetSqlColumnType(DbType.Time, null), Is.Not.Empty, name);
                Assert.Throws<NotImplementedException>(
                    () => adapter.GetSqlColumnType(DbType.Guid, null),
                    name);
            });
        }
    }

    [Test]
    public void Every_adapter_maps_bounded_and_large_string_sizes()
    {
        var table = new TableInfo(typeof(Device));
        var nameMember = TableMemberInfoSql.CreateSql(
            typeof(Device).GetProperty(nameof(Device.Name))!, table)!;
        var credentials = new StorageCredentials()
        {
            Host = "localhost",
            Username = "user",
            Password = "password",
            Database = "database"
        };
        var adapters = new dynamic[]
        {
            new SqliteStorage("adapter-sizes.db", false),
            new MySQLStorage(credentials, false),
            new PostgreSqlStorage(credentials, false),
            new MsSqlStorage(credentials, false)
        };

        foreach (dynamic adapter in adapters)
        {
            Assert.That(adapter.GetSqlColumnType(DbType.String, nameMember),
                Is.EqualTo("varchar(100)").IgnoreCase);
        }
    }
}
