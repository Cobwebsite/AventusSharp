using AventusSharp.Data;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;
using System.Data;

namespace AventusSharpTest.Integration.Containers;

[TestFixture]
[Category("Docker")]
[NonParallelizable]
public sealed class DockerDatabaseIntegrationTests
{
    [SetUp]
    public void RequireDocker()
    {
        DatabaseContainers.RequireDocker();
    }

    [Test]
    public async Task MySql_generates_schema_and_executes_crud_and_lambda_queries()
    {
        await VerifyProvider(
            "mysql_devices",
            () => MySqlDevice.CreateWithError(new MySqlDevice
            {
                Name = "MySQL lamp", Room = "Office", Value = 40, Enabled = true,
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Duration = new TimeSpan(12, 34, 56)
            }),
            () => MySqlDevice.GetAllWithError(),
            value => MySqlDevice.Where(device =>
                device.Room == "Office" && device.Value >= value && device.Enabled),
            device =>
            {
                device.Value = 70;
                return MySqlDevice.UpdateWithError(device);
            },
            device => MySqlDevice.DeleteWithError(device));
    }

    [Test]
    public async Task PostgreSql_generates_schema_and_executes_crud_and_lambda_queries()
    {
        await VerifyProvider(
            "postgresql_devices",
            () => PostgreSqlDevice.CreateWithError(new PostgreSqlDevice
            {
                Name = "PostgreSQL lamp", Room = "Office", Value = 40, Enabled = true,
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Duration = new TimeSpan(12, 34, 56)
            }),
            () => PostgreSqlDevice.GetAllWithError(),
            value => PostgreSqlDevice.Where(device =>
                device.Room == "Office" && device.Value >= value && device.Enabled),
            device =>
            {
                device.Value = 70;
                return PostgreSqlDevice.UpdateWithError(device);
            },
            device => PostgreSqlDevice.DeleteWithError(device));
    }

    [Test]
    public async Task SqlServer_generates_schema_and_executes_crud_and_lambda_queries()
    {
        await VerifyProvider(
            "mssql_devices",
            () => MsSqlDevice.CreateWithError(new MsSqlDevice
            {
                Name = "SQL Server lamp", Room = "Office", Value = 40, Enabled = true,
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Duration = new TimeSpan(12, 34, 56)
            }),
            () => MsSqlDevice.GetAllWithError(),
            value => MsSqlDevice.Where(device =>
                device.Room == "Office" && device.Value >= value && device.Enabled),
            device =>
            {
                device.Value = 70;
                return MsSqlDevice.UpdateWithError(device);
            },
            device => MsSqlDevice.DeleteWithError(device));
    }

    [TestCaseSource(nameof(ProviderSchemas))]
    public async Task Providers_generate_size_nullability_and_string_defaults(
        string tableName)
    {
        IDBStorage storage = tableName switch
        {
            "mysql_devices" => DatabaseContainers.MySql,
            "postgresql_devices" => DatabaseContainers.PostgreSql,
            "mssql_devices" => DatabaseContainers.MsSql,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        var result = await storage.Query(
            "SELECT CHARACTER_MAXIMUM_LENGTH AS max_length, " +
            "IS_NULLABLE AS is_nullable, COLUMN_DEFAULT AS default_value " +
            "FROM INFORMATION_SCHEMA.COLUMNS " +
            $"WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'Category';");
        var nameResult = await storage.Query(
            "SELECT CHARACTER_MAXIMUM_LENGTH AS max_length, " +
            "IS_NULLABLE AS is_nullable " +
            "FROM INFORMATION_SCHEMA.COLUMNS " +
            $"WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'Name';");

        Assert.That(result.Success, Is.True,
            string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.That(nameResult.Success, Is.True,
            string.Join(Environment.NewLine, nameResult.Errors.Select(error => error.Message)));
        var category = result.Result!.Single();
        var name = nameResult.Result!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(name["max_length"], Is.EqualTo("64"), $"{tableName}.Name size");
            Assert.That(name["is_nullable"], Is.EqualTo("NO").IgnoreCase,
                $"{tableName}.Name nullability");
            Assert.That(category["is_nullable"], Is.EqualTo("NO").IgnoreCase,
                $"{tableName}.Category nullability");
            Assert.That(category["default_value"], Does.Contain("standard").IgnoreCase,
                $"{tableName}.Category default");
        });
    }

    private static IEnumerable<TestCaseData> ProviderSchemas()
    {
        yield return new TestCaseData("mysql_devices")
            .SetName("MySQL_schema_contains_size_nullability_and_default");
        yield return new TestCaseData("postgresql_devices")
            .SetName("PostgreSQL_schema_contains_size_nullability_and_default");
        yield return new TestCaseData("mssql_devices")
            .SetName("SQL_Server_schema_contains_size_nullability_and_default");
    }

    [TestCaseSource(nameof(RawQueryProviders))]
    public async Task Providers_raw_query_preserves_null_unicode_and_row_order(
        string provider,
        string sql)
    {
        var storage = StorageFor(provider);

        var result = await storage.Query(sql);

        Assert.That(result.Success, Is.True,
            string.Join(Environment.NewLine,
                result.Errors.Select(error => error.Message)));
        Assert.That(result.Result, Has.Count.EqualTo(2));
        var rows = result.Result!;
        Assert.That(rows.Select(row => row["row_id"]),
            Is.EqualTo(new[] { "1", "2" }));
        Assert.That(rows[0]["value_text"], Is.Null);
        Assert.That(rows[1]["value_text"], Is.EqualTo("éclairage"));
    }

    [TestCaseSource(nameof(ProviderNames))]
    public async Task Providers_invalid_raw_query_is_monadic_and_connection_recovers(
        string provider)
    {
        var storage = StorageFor(provider);
        const string invalidSql =
            "SELECT missing_column FROM aventus_missing_table;";

        var invalid = await storage.Query(invalidSql);
        var afterFailure = await storage.Query("SELECT 1 AS value;");

        Assert.Multiple(() =>
        {
            Assert.That(invalid.Success, Is.False);
            Assert.That(invalid.Errors, Is.Not.Empty);
            Assert.That(invalid.Errors.SelectMany(error => error.Details),
                Does.Contain(invalidSql));
            Assert.That(afterFailure.Success, Is.True,
                string.Join(Environment.NewLine,
                    afterFailure.Errors.Select(error => error.Message)));
            Assert.That(afterFailure.Result!.Single()["value"], Is.EqualTo("1"));
        });
    }

    private static IEnumerable<TestCaseData> RawQueryProviders()
    {
        yield return new TestCaseData(
                "mysql",
                "SELECT 1 AS row_id, CAST(NULL AS CHAR) AS value_text " +
                "UNION ALL SELECT 2, 'éclairage' ORDER BY row_id;")
            .SetName("MySQL_raw_query_preserves_null_unicode_and_row_order");
        yield return new TestCaseData(
                "postgresql",
                "SELECT 1 AS row_id, CAST(NULL AS TEXT) AS value_text " +
                "UNION ALL SELECT 2, 'éclairage' ORDER BY row_id;")
            .SetName("PostgreSQL_raw_query_preserves_null_unicode_and_row_order");
        yield return new TestCaseData(
                "mssql",
                "SELECT 1 AS row_id, CAST(NULL AS NVARCHAR(32)) AS value_text " +
                "UNION ALL SELECT 2, N'éclairage' ORDER BY row_id;")
            .SetName("SQL_Server_raw_query_preserves_null_unicode_and_row_order");
    }

    private static IEnumerable<TestCaseData> ProviderNames()
    {
        yield return new TestCaseData("mysql")
            .SetName("MySQL_invalid_raw_query_is_monadic_and_connection_recovers");
        yield return new TestCaseData("postgresql")
            .SetName("PostgreSQL_invalid_raw_query_is_monadic_and_connection_recovers");
        yield return new TestCaseData("mssql")
            .SetName("SQL_Server_invalid_raw_query_is_monadic_and_connection_recovers");
    }

    private static IEnumerable<TestCaseData> ConnectedProviderNames()
    {
        yield return new TestCaseData("mysql")
            .SetName("MySQL_IsConnected_succeeds");
        yield return new TestCaseData("postgresql")
            .SetName("PostgreSQL_IsConnected_succeeds");
        yield return new TestCaseData("mssql")
            .SetName("SQL_Server_IsConnected_succeeds");
    }

    [TestCaseSource(nameof(ConnectedProviderNames))]
    public async Task Providers_report_that_the_database_is_connected(string provider)
    {
        IDBStorage storage = StorageFor(provider);

        var result = await storage.IsConnected();

        Assert.That(result.Success, Is.True,
            string.Join(Environment.NewLine,
                result.Errors.Select(error => error.Message)));
    }

    private static IDBStorage StorageFor(string provider) => provider switch
    {
        "mysql" => DatabaseContainers.MySql,
        "postgresql" => DatabaseContainers.PostgreSql,
        "mssql" => DatabaseContainers.MsSql,
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    [Test]
    public Task MySql_connection_failure_is_monadic_and_does_not_affect_valid_storage() =>
        VerifyConnectionFailure(
            new AventusSharp.Data.Storage.Mysql.MySQLStorage(
                new StorageCredentials(
                    "127.0.0.1", 1, "invalid", "invalid", "invalid"),
                createDatabase: false),
            DatabaseContainers.MySql);

    [Test]
    public Task PostgreSql_connection_failure_is_monadic_and_does_not_affect_valid_storage() =>
        VerifyConnectionFailure(
            new AventusSharp.Data.Storage.Postgresql.PostgreSqlStorage(
                new StorageCredentials(
                    "127.0.0.1", 1, "invalid", "invalid", "invalid"),
                createDatabase: false),
            DatabaseContainers.PostgreSql);

    [Test]
    public Task SqlServer_connection_failure_is_monadic_and_does_not_affect_valid_storage() =>
        VerifyConnectionFailure(
            new AventusSharp.Data.Storage.Mssql.MsSqlStorage(
                new StorageCredentials(
                    "127.0.0.1", 1, "invalid", "invalid", "master")
                {
                    trustServerCertificate = true
                }),
            DatabaseContainers.MsSql);

    private static async Task VerifyConnectionFailure(
        IDBStorage invalidStorage,
        IDBStorage validStorage)
    {
        VoidWithError? failedConnection = null;
        Assert.DoesNotThrowAsync(async () =>
        {
            failedConnection = await invalidStorage.ConnectWithError();
        });
        var validConnection = await validStorage.ConnectWithError();
        var validQuery = await validStorage.Query("SELECT 1 AS value;");

        Assert.Multiple(() =>
        {
            Assert.That(failedConnection, Is.Not.Null);
            Assert.That(failedConnection!.Success, Is.False);
            Assert.That(failedConnection.Errors, Is.Not.Empty);
            Assert.That(validConnection.Success, Is.True,
                string.Join(Environment.NewLine,
                    validConnection.Errors.Select(error => error.Message)));
            Assert.That(validQuery.Success, Is.True,
                string.Join(Environment.NewLine,
                    validQuery.Errors.Select(error => error.Message)));
            Assert.That(validQuery.Result!.Single()["value"], Is.EqualTo("1"));
        });
    }

    [Test]
    public Task MySql_parameterized_raw_query_reuses_command_without_altering_values() =>
        VerifyParameterizedRawQuery(
            (AventusSharp.Data.Storage.Mysql.MySQLStorage)
            DatabaseContainers.MySql);

    [Test]
    public Task PostgreSql_parameterized_raw_query_reuses_command_without_altering_values() =>
        VerifyParameterizedRawQuery(
            (AventusSharp.Data.Storage.Postgresql.PostgreSqlStorage)
            DatabaseContainers.PostgreSql);

    [Test]
    public Task SqlServer_parameterized_raw_query_reuses_command_without_altering_values() =>
        VerifyParameterizedRawQuery(
            (AventusSharp.Data.Storage.Mssql.MsSqlStorage)
            DatabaseContainers.MsSql);

    [Test]
    public Task MySql_typed_command_query_applies_SqlTransform() =>
        VerifyTypedTransformedQuery(
            (AventusSharp.Data.Storage.Mysql.MySQLStorage)
            DatabaseContainers.MySql);

    [Test]
    public Task PostgreSql_typed_command_query_applies_SqlTransform() =>
        VerifyTypedTransformedQuery(
            (AventusSharp.Data.Storage.Postgresql.PostgreSqlStorage)
            DatabaseContainers.PostgreSql);

    [Test]
    public Task SqlServer_typed_command_query_applies_SqlTransform() =>
        VerifyTypedTransformedQuery(
            (AventusSharp.Data.Storage.Mssql.MsSqlStorage)
            DatabaseContainers.MsSql);

    [Test]
    public Task MySql_missing_raw_parameter_does_not_reuse_the_previous_value() =>
        VerifyMissingRawParameter(
            (AventusSharp.Data.Storage.Mysql.MySQLStorage)
            DatabaseContainers.MySql);

    [Test]
    public Task PostgreSql_missing_raw_parameter_does_not_reuse_the_previous_value() =>
        VerifyMissingRawParameter(
            (AventusSharp.Data.Storage.Postgresql.PostgreSqlStorage)
            DatabaseContainers.PostgreSql);

    [Test]
    public Task SqlServer_missing_raw_parameter_does_not_reuse_the_previous_value() =>
        VerifyMissingRawParameter(
            (AventusSharp.Data.Storage.Mssql.MsSqlStorage)
            DatabaseContainers.MsSql);

    private static async Task VerifyParameterizedRawQuery<T>(
        DefaultDBStorage<T> storage)
        where T : IDBStorage
    {
        var commandResult = storage.CreateCmd(
            "SELECT @value AS value_text, @nullable AS null_text;");
        Assert.That(commandResult.Success, Is.True,
            string.Join(Environment.NewLine,
                commandResult.Errors.Select(error => error.Message)));
        Assert.That(commandResult.Result, Is.Not.Null);
        using var command = commandResult.Result!;

        var valueParameter = storage.GetDbParameter();
        valueParameter.ParameterName = "@value";
        valueParameter.DbType = DbType.String;
        command.Parameters.Add(valueParameter);
        var nullableParameter = storage.GetDbParameter();
        nullableParameter.ParameterName = "@nullable";
        nullableParameter.DbType = DbType.String;
        command.Parameters.Add(nullableParameter);

        var parameters = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["@value"] = "O'Reilly; --",
                ["@nullable"] = null
            },
            new()
            {
                ["@value"] = "éclairage 東京",
                ["@nullable"] = "present"
            }
        };

        var result = await storage.Query(command, parameters);

        Assert.That(result.Success, Is.True,
            string.Join(Environment.NewLine,
                result.Errors.Select(error => error.Message)));
        Assert.That(result.Result, Has.Count.EqualTo(2));
        Assert.That(result.Result![0]["value_text"], Is.EqualTo("O'Reilly; --"));
        Assert.That(result.Result[0]["null_text"], Is.Null);
        Assert.That(result.Result[1]["value_text"], Is.EqualTo("éclairage 東京"));
        Assert.That(result.Result[1]["null_text"], Is.EqualTo("present"));
    }

    private static async Task VerifyTypedTransformedQuery<T>(
        DefaultDBStorage<T> storage)
        where T : IDBStorage
    {
        var commandResult = storage.CreateCmd(
            "SELECT @id AS \"Id\", @name AS \"Name\", " +
            "@deleted AS \"Deleted\";");
        Assert.That(commandResult.Success, Is.True,
            string.Join(Environment.NewLine,
                commandResult.Errors.Select(error => error.Message)));
        Assert.That(commandResult.Result, Is.Not.Null);
        using var command = commandResult.Result!;

        foreach ((string name, DbType type) in new[]
        {
            ("@id", DbType.Int32),
            ("@name", DbType.String),
            ("@deleted", DbType.String)
        })
        {
            var parameter = storage.GetDbParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            command.Parameters.Add(parameter);
        }

        var result = await storage.Query<TransformedBoolRecord>(command,
        [
            new Dictionary<string, object?>
                { ["@id"] = 901, ["@name"] = "Active", ["@deleted"] = "N" },
            new Dictionary<string, object?>
                { ["@id"] = 902, ["@name"] = "Deleted", ["@deleted"] = "Y" }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True,
                string.Join(Environment.NewLine,
                    result.Errors.Select(error => error.Message)));
            Assert.That(result.Result, Has.Count.EqualTo(2));
            Assert.That(result.Result![0].Id, Is.EqualTo(901));
            Assert.That(result.Result[0].Deleted, Is.False);
            Assert.That(result.Result[1].Id, Is.EqualTo(902));
            Assert.That(result.Result[1].Deleted, Is.True);
        });
    }

    private static async Task VerifyMissingRawParameter<T>(
        DefaultDBStorage<T> storage)
        where T : IDBStorage
    {
        var commandResult = storage.CreateCmd(
            "SELECT @value AS value_text, @required AS required_text;");
        Assert.That(commandResult.Success, Is.True,
            string.Join(Environment.NewLine,
                commandResult.Errors.Select(error => error.Message)));
        Assert.That(commandResult.Result, Is.Not.Null);
        using var command = commandResult.Result!;

        var valueParameter = storage.GetDbParameter();
        valueParameter.ParameterName = "@value";
        valueParameter.DbType = DbType.String;
        command.Parameters.Add(valueParameter);
        var requiredParameter = storage.GetDbParameter();
        requiredParameter.ParameterName = "@required";
        requiredParameter.DbType = DbType.String;
        command.Parameters.Add(requiredParameter);

        var result = await storage.Query(command,
        [
            new Dictionary<string, object?>
            {
                ["@value"] = "first",
                ["@required"] = "must-not-leak"
            },
            new Dictionary<string, object?>
            {
                ["@value"] = "second"
            }
        ]);
        var afterFailure = await storage.Query("SELECT 1 AS value;");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False,
                "An incomplete parameter set must not reuse a value from the previous execution.");
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(afterFailure.Success, Is.True,
                string.Join(Environment.NewLine,
                    afterFailure.Errors.Select(error => error.Message)));
            Assert.That(afterFailure.Result!.Single()["value"], Is.EqualTo("1"));
        });
    }

    [Test]
    public Task MySql_rolls_back_manager_transactions()
    {
        return VerifyRollback(
            () => MySqlDevice.CreateWithError(new MySqlDevice
            {
                Name = "mysql rollback", Room = "Lab",
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }),
            () => ((MySqlDeviceManager)GenericDM.Get<MySqlDevice>())
                .WhereWithErrorNoCache<MySqlDevice>(device => device.Name == "mysql rollback"));
    }

    [Test]
    public Task PostgreSql_rolls_back_manager_transactions()
    {
        return VerifyRollback(
            () => PostgreSqlDevice.CreateWithError(new PostgreSqlDevice
            {
                Name = "postgres rollback", Room = "Lab",
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }),
            () => ((PostgreSqlDeviceManager)GenericDM.Get<PostgreSqlDevice>())
                .WhereWithErrorNoCache<PostgreSqlDevice>(device => device.Name == "postgres rollback"));
    }

    [Test]
    public Task SqlServer_rolls_back_manager_transactions()
    {
        return VerifyRollback(
            () => MsSqlDevice.CreateWithError(new MsSqlDevice
            {
                Name = "mssql rollback", Room = "Lab",
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }),
            () => ((MsSqlDeviceManager)GenericDM.Get<MsSqlDevice>())
                .WhereWithErrorNoCache<MsSqlDevice>(device => device.Name == "mssql rollback"));
    }

    [Test]
    public async Task Rollback_removes_a_created_item_from_the_local_cache()
    {
        var manager = GenericDM.Get<MySqlDevice>();
        MySqlDevice? created = null;
        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var result = await MySqlDevice.CreateWithError(new MySqlDevice
            {
                Name = "cache rollback",
                Room = "Lab",
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            created = result.Result;
            result.Errors.Add(new GenericError(9902, "forced cache rollback"));
            return result;
        });

        Assert.That(transaction.Success, Is.False);
        Assert.That(created, Is.Not.Null);

        var cachedLookup = await manager.GetByIdWithError<MySqlDevice>(created!.Id);
        Assert.That(cachedLookup.Result, Is.Null,
            "The rolled-back item must not remain addressable through preferLocalCache.");
    }

    [Test]
    public Task MySql_nested_outer_rollback_includes_operations_after_inner_commit() =>
        VerifyNestedRollback<MySqlDevice>();

    [Test]
    public Task PostgreSql_nested_outer_rollback_includes_operations_after_inner_commit() =>
        VerifyNestedRollback<PostgreSqlDevice>();

    [Test]
    public Task SqlServer_nested_outer_rollback_includes_operations_after_inner_commit() =>
        VerifyNestedRollback<MsSqlDevice>();

    [Test]
    public Task MySql_concurrent_transactions_keep_scopes_and_cache_isolated() =>
        VerifyConcurrentTransactions<MySqlDevice>(DatabaseContainers.MySql);

    [Test]
    public Task PostgreSql_concurrent_transactions_keep_scopes_and_cache_isolated() =>
        VerifyConcurrentTransactions<PostgreSqlDevice>(DatabaseContainers.PostgreSql);

    [Test]
    public Task SqlServer_concurrent_transactions_keep_scopes_and_cache_isolated() =>
        VerifyConcurrentTransactions<MsSqlDevice>(DatabaseContainers.MsSql);

    [Test]
    [Repeat(5)]
    public Task MySql_concurrent_updates_isolate_rollback_callbacks() =>
        VerifyConcurrentUpdates<MySqlDevice>(DatabaseContainers.MySql);

    [Test]
    [Repeat(5)]
    public Task PostgreSql_concurrent_updates_isolate_rollback_callbacks() =>
        VerifyConcurrentUpdates<PostgreSqlDevice>(DatabaseContainers.PostgreSql);

    [Test]
    [Repeat(5)]
    public Task SqlServer_concurrent_updates_isolate_rollback_callbacks() =>
        VerifyConcurrentUpdates<MsSqlDevice>(DatabaseContainers.MsSql);

    [Test]
    public Task MySql_invalid_sql_rolls_back_cache_and_allows_the_next_transaction() =>
        VerifyInvalidSqlRecovery<MySqlDevice>(
            DatabaseContainers.MySql,
            () => ((AventusSharp.Data.Storage.Mysql.MySQLStorage)
                    DatabaseContainers.MySql)
                .Execute("INSERT INTO table_that_does_not_exist (value) VALUES (1);"));

    [Test]
    public Task PostgreSql_invalid_sql_rolls_back_cache_and_allows_the_next_transaction() =>
        VerifyInvalidSqlRecovery<PostgreSqlDevice>(
            DatabaseContainers.PostgreSql,
            () => ((AventusSharp.Data.Storage.Postgresql.PostgreSqlStorage)
                    DatabaseContainers.PostgreSql)
                .Execute("INSERT INTO table_that_does_not_exist (value) VALUES (1);"));

    [Test]
    public Task SqlServer_invalid_sql_rolls_back_cache_and_allows_the_next_transaction() =>
        VerifyInvalidSqlRecovery<MsSqlDevice>(
            DatabaseContainers.MsSql,
            () => ((AventusSharp.Data.Storage.Mssql.MsSqlStorage)
                    DatabaseContainers.MsSql)
                .Execute("INSERT INTO table_that_does_not_exist (value) VALUES (1);"));

    [Test]
    public Task MySql_stream_callback_exception_is_monadic_and_releases_the_connection()
    {
        var storage = (AventusSharp.Data.Storage.Mysql.MySQLStorage)
            DatabaseContainers.MySql;
        return VerifyStreamCallbackException(
            storage.QueryStream,
            storage.Query);
    }

    [Test]
    public Task PostgreSql_stream_callback_exception_is_monadic_and_releases_the_connection()
    {
        var storage = (AventusSharp.Data.Storage.Postgresql.PostgreSqlStorage)
            DatabaseContainers.PostgreSql;
        return VerifyStreamCallbackException(
            storage.QueryStream,
            storage.Query);
    }

    [Test]
    public Task SqlServer_stream_callback_exception_is_monadic_and_releases_the_connection()
    {
        var storage = (AventusSharp.Data.Storage.Mssql.MsSqlStorage)
            DatabaseContainers.MsSql;
        return VerifyStreamCallbackException(
            storage.QueryStream,
            storage.Query);
    }

    [Test]
    public Task MySql_stream_callback_error_stops_the_stream_and_releases_the_connection()
    {
        var storage = (AventusSharp.Data.Storage.Mysql.MySQLStorage)
            DatabaseContainers.MySql;
        return VerifyStreamCallbackError(storage.QueryStream, storage.Query);
    }

    [Test]
    public Task PostgreSql_stream_callback_error_stops_the_stream_and_releases_the_connection()
    {
        var storage = (AventusSharp.Data.Storage.Postgresql.PostgreSqlStorage)
            DatabaseContainers.PostgreSql;
        return VerifyStreamCallbackError(storage.QueryStream, storage.Query);
    }

    [Test]
    public Task SqlServer_stream_callback_error_stops_the_stream_and_releases_the_connection()
    {
        var storage = (AventusSharp.Data.Storage.Mssql.MsSqlStorage)
            DatabaseContainers.MsSql;
        return VerifyStreamCallbackError(storage.QueryStream, storage.Query);
    }

    [Test]
    public Task MySql_read_only_mode_rejects_writes_and_keeps_the_connection_usable()
    {
        var storage = (AventusSharp.Data.Storage.Mysql.MySQLStorage)
            DatabaseContainers.MySql;
        return VerifyReadOnlyMode(
            value => storage.ReadOnly = value,
            storage.Execute,
            storage.Query);
    }

    [Test]
    public Task PostgreSql_read_only_mode_rejects_writes_and_keeps_the_connection_usable()
    {
        var storage = (AventusSharp.Data.Storage.Postgresql.PostgreSqlStorage)
            DatabaseContainers.PostgreSql;
        return VerifyReadOnlyMode(
            value => storage.ReadOnly = value,
            storage.Execute,
            storage.Query);
    }

    [Test]
    public Task SqlServer_read_only_mode_rejects_writes_and_keeps_the_connection_usable()
    {
        var storage = (AventusSharp.Data.Storage.Mssql.MsSqlStorage)
            DatabaseContainers.MsSql;
        return VerifyReadOnlyMode(
            value => storage.ReadOnly = value,
            storage.Execute,
            storage.Query);
    }

    [Test]
    public Task MySql_supports_direct_relations_and_cascade() =>
        VerifyDirectRelation(
            (MySqlRelationChild child) => child.Parent,
            parent => new MySqlRelationChild { Name = "MySQL child", Parent = parent },
            (child, parent) => child.Parent = parent);

    [Test]
    public Task PostgreSql_supports_direct_relations_and_cascade() =>
        VerifyDirectRelation(
            (PostgreSqlRelationChild child) => child.Parent,
            parent => new PostgreSqlRelationChild
            {
                Name = "PostgreSQL child",
                Parent = parent
            },
            (child, parent) => child.Parent = parent);

    [Test]
    public Task SqlServer_supports_direct_relations_and_cascade() =>
        VerifyDirectRelation(
            (MsSqlRelationChild child) => child.Parent,
            parent => new MsSqlRelationChild { Name = "SQL Server child", Parent = parent },
            (child, parent) => child.Parent = parent);

    [Test]
    public Task MySql_supports_many_to_many_relation_lifecycle() =>
        VerifyManyToManyRelation<
            MySqlRelationParent,
            MySqlRelationChild,
            MySqlRelationGroup>(
            parent => new MySqlRelationChild { Name = "MySQL linked child", Parent = parent },
            group => group.Children,
            (group, children) => group.Children = children);

    [Test]
    public Task PostgreSql_supports_many_to_many_relation_lifecycle() =>
        VerifyManyToManyRelation<
            PostgreSqlRelationParent,
            PostgreSqlRelationChild,
            PostgreSqlRelationGroup>(
            parent => new PostgreSqlRelationChild
            {
                Name = "PostgreSQL linked child",
                Parent = parent
            },
            group => group.Children,
            (group, children) => group.Children = children);

    [Test]
    public Task SqlServer_supports_many_to_many_relation_lifecycle() =>
        VerifyManyToManyRelation<
            MsSqlRelationParent,
            MsSqlRelationChild,
            MsSqlRelationGroup>(
            parent => new MsSqlRelationChild
            {
                Name = "SQL Server linked child",
                Parent = parent
            },
            group => group.Children,
            (group, children) => group.Children = children);

    private static async Task VerifyProvider<T>(
        string expectedTable,
        Func<Task<ResultWithError<T>>> create,
        Func<Task<ResultWithError<List<T>>>> getAll,
        Func<int, Task<List<T>>> query,
        Func<T, Task<ResultWithError<T>>> update,
        Func<T, Task<ResultWithError<T>>> delete)
        where T : class, IStorable
    {
        var createResult = await create();
        var errorMessage = string.Join(Environment.NewLine,
            createResult.Errors.Select(error => error.GetMessageException(true)));
        Assert.That(createResult.Success, Is.True, $"{expectedTable}: create failed{Environment.NewLine}{errorMessage}");
        var created = createResult.Result;
        Assert.That(created, Is.Not.Null, $"{expectedTable}: create returned no item");
        Assert.That(created!.Id, Is.GreaterThan(0), $"{expectedTable}: no generated id");
        Assert.That(created, Is.InstanceOf<IStorableTimestamp>());
        var timestamped = (IStorableTimestamp)created;
        var createdDate = timestamped.CreatedDate;
        var firstUpdatedDate = timestamped.UpdatedDate;
        Assert.That(createdDate, Is.GreaterThan(DateTime.MinValue), $"{expectedTable}: no creation timestamp");
        Assert.That(firstUpdatedDate, Is.GreaterThan(DateTime.MinValue), $"{expectedTable}: no update timestamp");

        var getAllResult = await getAll();
        var getAllErrors = string.Join(Environment.NewLine,
            getAllResult.Errors.Select(error => error.GetMessageException(true)));
        Assert.That(getAllResult.Success, Is.True,
            $"{expectedTable}: get-all failed{Environment.NewLine}{getAllErrors}");
        var all = getAllResult.Result ?? [];
        Assert.That(all.Any(item => item.Id == created.Id), Is.True,
            $"{expectedTable}: generated schema/query failed; created id={created.Id}; "
            + $"loaded ids=[{string.Join(",", all.Select(item => item.Id))}]");
        Assert.That(((IContainerDevice)all.Single(item => item.Id == created.Id)).Duration,
            Is.EqualTo(new TimeSpan(12, 34, 56)),
            $"{expectedTable}: TimeSpan did not round-trip");

        var filtered = await query(30);
        Assert.That(filtered.Any(item => item.Id == created.Id), Is.True,
            $"{expectedTable}: LambdaTranslator query failed");

        await Task.Delay(20);
        var updateResult = await update(created);
        Assert.That(updateResult.Success, Is.True, $"{expectedTable}: update failed{Environment.NewLine}"
            + string.Join(Environment.NewLine, updateResult.Errors.Select(error => error.GetMessageException(true))));
        Assert.That(timestamped.CreatedDate, Is.EqualTo(createdDate),
            $"{expectedTable}: update changed CreatedDate");
        Assert.That(timestamped.UpdatedDate, Is.GreaterThan(firstUpdatedDate),
            $"{expectedTable}: update did not advance UpdatedDate");

        var deleteResult = await delete(created);
        Assert.That(deleteResult.Success, Is.True, $"{expectedTable}: delete failed{Environment.NewLine}"
            + string.Join(Environment.NewLine, deleteResult.Errors.Select(error => error.GetMessageException(true))));
    }

    private static async Task VerifyRollback<T>(
        Func<Task<ResultWithError<T>>> create,
        Func<Task<ResultWithError<List<T>>>> find)
        where T : class, IStorable
    {
        var manager = GenericDM.Get<T>();
        var transactionResult = await manager.RunInsideTransaction(async () =>
        {
            var result = await create();
            result.Errors.Add(new GenericError(9901, "forced rollback"));
            return result;
        });

        Assert.That(transactionResult.Success, Is.False);

        var queryResult = await find();
        Assert.That(queryResult.Success, Is.True,
            string.Join(Environment.NewLine, queryResult.Errors.Select(error => error.GetMessageException(true))));
        Assert.That(queryResult.Result, Is.Empty, "The row created in the failed transaction was persisted.");
    }

    private static async Task VerifyNestedRollback<T>()
        where T : class, IStorable, IContainerDevice, new()
    {
        var manager = GenericDM.Get<T>();
        var cleanup = await manager.CreateDelete<T>()
            .Where(item => item.Id > 0)
            .RunWithError();
        Assert.That(cleanup.Success, Is.True,
            string.Join(Environment.NewLine, cleanup.Errors.Select(error => error.Message)));

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var first = await manager.CreateWithError(NewNestedDevice<T>("Outer before"));
            if (!first.Success)
                return first;

            var inner = await manager.RunInsideTransaction(
                () => manager.CreateWithError(NewNestedDevice<T>("Inner")));
            if (!inner.Success)
                return inner;

            var last = await manager.CreateWithError(NewNestedDevice<T>("Outer after"));
            last.Errors.Add(new GenericError(9921, "force nested rollback"));
            return last;
        });
        var loaded = await manager.CreateQuery<T>()
            .Where(item => item.Room == "Nested")
            .RunWithError();

        Assert.That(transaction.Success, Is.False);
        Assert.That(loaded.Success, Is.True,
            string.Join(Environment.NewLine, loaded.Errors.Select(error => error.Message)));
        Assert.That(loaded.Result, Is.Empty);
    }

    private static T NewNestedDevice<T>(string name)
        where T : class, IContainerDevice, new() =>
        new()
        {
            Name = name,
            Category = "nested",
            Room = "Nested",
            Value = 1,
            Enabled = true,
            InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Duration = TimeSpan.FromMinutes(1)
        };

    private static async Task VerifyConcurrentTransactions<T>(IDBStorage storage)
        where T : class, IStorable, IContainerDevice, new()
    {
        var manager = GenericDM.Get<T>();
        var prefix = $"concurrent-{Guid.NewGuid():N}";
        var rollbackItem = NewConcurrentDevice<T>($"{prefix}-rollback");
        var commitItem = NewConcurrentDevice<T>($"{prefix}-commit");
        var rollbackReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var commitReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TransactionContext? rollbackContext = null;
        TransactionContext? commitContext = null;

        var rollbackTask = Task.Run(() =>
            manager.RunInsideTransaction(async () =>
            {
                rollbackContext = storage.getTransactionScope();
                rollbackReady.SetResult();
                await commitReady.Task;
                var creation = await manager.CreateWithError(rollbackItem);
                creation.Errors.Add(new GenericError(
                    9903, "force isolated concurrent rollback"));
                return creation;
            }));
        var commitTask = Task.Run(() =>
            manager.RunInsideTransaction(async () =>
            {
                commitContext = storage.getTransactionScope();
                commitReady.SetResult();
                await rollbackReady.Task;
                return await manager.CreateWithError(commitItem);
            }));

        var results = await Task.WhenAll(rollbackTask, commitTask);
        var persisted = await manager.CreateQuery<T>()
            .Where(item => item.Id == rollbackItem.Id || item.Id == commitItem.Id)
            .RunWithError();
        var rolledBackCached = await manager.GetByIdWithError<T>(rollbackItem.Id);
        var committedCached = await manager.GetByIdWithError<T>(commitItem.Id);

        Assert.Multiple(() =>
        {
            Assert.That(rollbackContext, Is.Not.Null);
            Assert.That(commitContext, Is.Not.Null);
            Assert.That(commitContext, Is.Not.SameAs(rollbackContext));
            Assert.That(results[0].Success, Is.False);
            Assert.That(results[1].Success, Is.True,
                string.Join(Environment.NewLine,
                    results[1].Errors.Select(error => error.Message)));
            Assert.That(persisted.Success, Is.True,
                string.Join(Environment.NewLine,
                    persisted.Errors.Select(error => error.Message)));
            Assert.That(persisted.Result!.Select(item => item.Id),
                Is.EqualTo(new[] { commitItem.Id }));
            Assert.That(rolledBackCached.Success, Is.False);
            Assert.That(rolledBackCached.Result, Is.Null);
            Assert.That(committedCached.Success, Is.True);
            Assert.That(committedCached.Result, Is.SameAs(commitItem));
        });
    }

    private static T NewConcurrentDevice<T>(string name)
        where T : class, IContainerDevice, new() =>
        new()
        {
            Name = name,
            Category = "concurrent",
            Room = "Concurrent",
            Value = 1,
            Enabled = true,
            InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Duration = TimeSpan.FromSeconds(1)
        };

    private static async Task VerifyConcurrentUpdates<T>(IDBStorage storage)
        where T : class, IStorable, IContainerDevice, new()
    {
        var manager = GenericDM.Get<T>();
        var prefix = $"concurrent-update-{Guid.NewGuid():N}";
        var rollbackItem = NewConcurrentDevice<T>($"{prefix}-rollback");
        var commitItem = NewConcurrentDevice<T>($"{prefix}-commit");
        var initialCreation = await manager.CreateWithError([rollbackItem, commitItem]);
        Assert.That(initialCreation.Success, Is.True,
            string.Join(Environment.NewLine,
                initialCreation.Errors.Select(error => error.Message)));

        var rollbackReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var commitReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TransactionContext? rollbackContext = null;
        TransactionContext? commitContext = null;

        var rollbackTask = Task.Run(() =>
            manager.RunInsideTransaction(async () =>
            {
                rollbackContext = storage.getTransactionScope();
                rollbackItem.Value = 10;
                rollbackReady.SetResult();
                await commitReady.Task;
                var update = await manager.UpdateWithError(rollbackItem);
                update.Errors.Add(new GenericError(
                    9904, "force isolated concurrent update rollback"));
                return update;
            }));
        var commitTask = Task.Run(() =>
            manager.RunInsideTransaction(async () =>
            {
                commitContext = storage.getTransactionScope();
                commitItem.Value = 20;
                commitReady.SetResult();
                await rollbackReady.Task;
                return await manager.UpdateWithError(commitItem);
            }));

        var results = await Task.WhenAll(rollbackTask, commitTask);
        var persisted = await manager.CreateQuery<T>()
            .Where(item => item.Id == rollbackItem.Id || item.Id == commitItem.Id)
            .RunWithError();
        var persistedRollback = persisted.Result!
            .Single(item => item.Id == rollbackItem.Id);
        var persistedCommit = persisted.Result!
            .Single(item => item.Id == commitItem.Id);
        var cachedRollback = await manager.GetByIdWithError<T>(rollbackItem.Id);
        var cachedCommit = await manager.GetByIdWithError<T>(commitItem.Id);

        Assert.Multiple(() =>
        {
            Assert.That(rollbackContext, Is.Not.Null);
            Assert.That(commitContext, Is.Not.Null);
            Assert.That(commitContext, Is.Not.SameAs(rollbackContext));
            Assert.That(results[0].Success, Is.False);
            Assert.That(results[1].Success, Is.True,
                string.Join(Environment.NewLine,
                    results[1].Errors.Select(error => error.Message)));
            Assert.That(persisted.Success, Is.True,
                string.Join(Environment.NewLine,
                    persisted.Errors.Select(error => error.Message)));
            Assert.That(persistedRollback.Value, Is.EqualTo(1));
            Assert.That(persistedCommit.Value, Is.EqualTo(20));
            Assert.That(cachedRollback.Result, Is.SameAs(rollbackItem));
            Assert.That(cachedCommit.Result, Is.SameAs(commitItem));
            Assert.That(rollbackItem.Value, Is.EqualTo(1));
            Assert.That(commitItem.Value, Is.EqualTo(20));
        });
    }

    private static async Task VerifyInvalidSqlRecovery<T>(
        IDBStorage storage,
        Func<Task<VoidWithError>> executeInvalidSql)
        where T : class, IStorable, IContainerDevice, new()
    {
        var manager = GenericDM.Get<T>();
        var prefix = $"invalid-sql-{Guid.NewGuid():N}";
        var rolledBackItem = NewConcurrentDevice<T>($"{prefix}-rollback");

        var failed = await manager.RunInsideTransaction(async () =>
        {
            var creation = await manager.CreateWithError(rolledBackItem);
            if (!creation.Success)
                return creation;
            var invalid = await executeInvalidSql();
            creation.Errors.AddRange(invalid.Errors);
            return creation;
        });
        var persistedAfterFailure = await manager.CreateQuery<T>()
            .Where(item => item.Name == rolledBackItem.Name)
            .RunWithError();
        var cachedAfterFailure = await manager
            .GetByIdWithError<T>(rolledBackItem.Id);
        var nextItem = NewConcurrentDevice<T>($"{prefix}-next");
        var next = await manager.RunInsideTransaction(
            () => manager.CreateWithError(nextItem));

        Assert.Multiple(() =>
        {
            Assert.That(failed.Success, Is.False);
            Assert.That(persistedAfterFailure.Success, Is.True,
                string.Join(Environment.NewLine,
                    persistedAfterFailure.Errors.Select(error => error.Message)));
            Assert.That(persistedAfterFailure.Result, Is.Empty);
            Assert.That(cachedAfterFailure.Success, Is.False);
            Assert.That(cachedAfterFailure.Result, Is.Null);
            Assert.That(next.Success, Is.True,
                string.Join(Environment.NewLine,
                    next.Errors.Select(error => error.Message)));
            Assert.That(next.Result, Is.SameAs(nextItem));
            Assert.That(storage.getTransactionScope(), Is.Null);
        });
    }

    private static async Task VerifyStreamCallbackException(
        Func<string, Func<Dictionary<string, string?>, Task<VoidWithError>>,
            string, int, Task<VoidWithError>> queryStream,
        Func<string, string, int,
            Task<ResultWithError<List<Dictionary<string, string?>>>>> query)
    {
        var visited = new List<string?>();
        var stream = await queryStream(
            "SELECT 1 AS id UNION ALL SELECT 2 AS id UNION ALL SELECT 3 AS id;",
            row =>
            {
                visited.Add(row["id"]);
                if (row["id"] == "2")
                    throw new InvalidOperationException("stream callback exception");
                return Task.FromResult(new VoidWithError());
            },
            "",
            0);
        var afterFailure = await query("SELECT 1 AS value;", "", 0);

        Assert.Multiple(() =>
        {
            Assert.That(stream.Success, Is.False);
            Assert.That(string.Join(Environment.NewLine,
                    stream.Errors.Select(error => error.Message)),
                Does.Contain("stream callback exception"));
            Assert.That(visited, Is.EqualTo(new[] { "1", "2" }));
            Assert.That(afterFailure.Success, Is.True,
                string.Join(Environment.NewLine,
                    afterFailure.Errors.Select(error => error.Message)));
            Assert.That(afterFailure.Result!.Single()["value"], Is.EqualTo("1"));
        });
    }

    private static async Task VerifyReadOnlyMode(
        Action<bool> setReadOnly,
        Func<string, string, int, Task<VoidWithError>> execute,
        Func<string, string, int,
            Task<ResultWithError<List<Dictionary<string, string?>>>>> query)
    {
        ResultWithError<List<Dictionary<string, string?>>>? read = null;
        VoidWithError? rejectedWrite = null;
        try
        {
            setReadOnly(true);
            read = await query("SELECT 1 AS value;", "", 0);
            rejectedWrite = await execute(
                "CREATE TABLE aventus_read_only_probe (id INT);", "", 0);
        }
        finally
        {
            setReadOnly(false);
        }

        var afterReadOnly = await query("SELECT 2 AS value;", "", 0);

        Assert.Multiple(() =>
        {
            Assert.That(read!.Success, Is.True,
                string.Join(Environment.NewLine,
                    read.Errors.Select(error => error.Message)));
            Assert.That(read.Result!.Single()["value"], Is.EqualTo("1"));
            Assert.That(rejectedWrite!.Success, Is.False);
            Assert.That(rejectedWrite.Errors.OfType<DataError>()
                    .Select(error => error.Code),
                Does.Contain(DataErrorCode.IsReadOnly));
            Assert.That(afterReadOnly.Success, Is.True,
                string.Join(Environment.NewLine,
                    afterReadOnly.Errors.Select(error => error.Message)));
            Assert.That(afterReadOnly.Result!.Single()["value"], Is.EqualTo("2"));
        });
    }

    private static async Task VerifyStreamCallbackError(
        Func<string, Func<Dictionary<string, string?>, Task<VoidWithError>>,
            string, int, Task<VoidWithError>> queryStream,
        Func<string, string, int,
            Task<ResultWithError<List<Dictionary<string, string?>>>>> query)
    {
        var visited = new List<string?>();
        var stream = await queryStream(
            "SELECT 1 AS id UNION ALL SELECT 2 AS id UNION ALL SELECT 3 AS id;",
            row =>
            {
                visited.Add(row["id"]);
                var result = new VoidWithError();
                if (row["id"] == "2")
                {
                    result.Errors.Add(new DataError(
                        DataErrorCode.ValidationError,
                        "stop Docker stream"));
                }
                return Task.FromResult(result);
            },
            "",
            0);
        var afterFailure = await query("SELECT 1 AS value;", "", 0);

        Assert.Multiple(() =>
        {
            Assert.That(stream.Success, Is.False);
            Assert.That(stream.Errors.OfType<DataError>()
                    .Select(error => error.Code),
                Does.Contain(DataErrorCode.ValidationError));
            Assert.That(visited, Is.EqualTo(new[] { "1", "2" }));
            Assert.That(afterFailure.Success, Is.True,
                string.Join(Environment.NewLine,
                    afterFailure.Errors.Select(error => error.Message)));
            Assert.That(afterFailure.Result!.Single()["value"], Is.EqualTo("1"));
        });
    }

    private static async Task VerifyDirectRelation<TParent, TChild>(
        Func<TChild, TParent> getParent,
        Func<TParent, TChild> createChild,
        Action<TChild, TParent> setParent)
        where TParent : class, IStorable, new()
        where TChild : class, IStorable
    {
        var parentManager = GenericDM.Get<TParent>();
        var childManager = GenericDM.Get<TChild>();
        var deleteChildren = await childManager.CreateDelete<TChild>()
            .Where(item => item.Id > 0)
            .RunWithError();
        var deleteParents = await parentManager.CreateDelete<TParent>()
            .Where(item => item.Id > 0)
            .RunWithError();
        Assert.That(deleteChildren.Success, Is.True,
            string.Join(Environment.NewLine, deleteChildren.Errors.Select(error => error.Message)));
        Assert.That(deleteParents.Success, Is.True,
            string.Join(Environment.NewLine, deleteParents.Errors.Select(error => error.Message)));

        var firstParent = new TParent();
        typeof(TParent).GetProperty("Name")!.SetValue(firstParent, "First parent");
        var secondParent = new TParent();
        typeof(TParent).GetProperty("Name")!.SetValue(secondParent, "Second parent");
        var firstCreation = await parentManager.CreateWithError(firstParent);
        var secondCreation = await parentManager.CreateWithError(secondParent);
        Assert.That(firstCreation.Success, Is.True,
            string.Join(Environment.NewLine, firstCreation.Errors.Select(error => error.Message)));
        Assert.That(secondCreation.Success, Is.True,
            string.Join(Environment.NewLine, secondCreation.Errors.Select(error => error.Message)));

        var child = createChild(firstParent);
        var childCreation = await childManager.CreateWithError(child);
        Assert.That(childCreation.Success, Is.True,
            string.Join(Environment.NewLine, childCreation.Errors.Select(error => error.Message)));

        var initiallyLoaded = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == child.Id)
            .SingleWithError();
        Assert.That(initiallyLoaded.Success, Is.True,
            string.Join(Environment.NewLine, initiallyLoaded.Errors.Select(error => error.Message)));
        Assert.That(getParent(initiallyLoaded.Result!).Id, Is.EqualTo(firstParent.Id));

        setParent(child, secondParent);
        var update = await childManager.UpdateWithError(child);
        var afterUpdate = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == child.Id)
            .SingleWithError();
        Assert.That(update.Success, Is.True,
            string.Join(Environment.NewLine, update.Errors.Select(error => error.Message)));
        Assert.That(afterUpdate.Success, Is.True,
            string.Join(Environment.NewLine, afterUpdate.Errors.Select(error => error.Message)));
        Assert.That(getParent(afterUpdate.Result!).Id, Is.EqualTo(secondParent.Id));

        var invalidParent = new TParent { Id = 999_999 };
        setParent(child, invalidParent);
        var invalidUpdate = await childManager.UpdateWithError(child);
        var afterInvalidUpdate = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == child.Id)
            .SingleWithError();
        Assert.That(invalidUpdate.Success, Is.False);
        Assert.That(afterInvalidUpdate.Success, Is.True,
            string.Join(Environment.NewLine,
                afterInvalidUpdate.Errors.Select(error => error.Message)));
        Assert.That(getParent(afterInvalidUpdate.Result!).Id, Is.EqualTo(secondParent.Id));
        Assert.That(getParent(child), Is.SameAs(secondParent),
            "A failed direct relation update must restore the canonical relation.");

        var parentDeletion = await parentManager.DeleteWithError(secondParent);
        var childAfterCascade = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == child.Id)
            .SingleWithError();
        Assert.That(parentDeletion.Success, Is.True,
            string.Join(Environment.NewLine, parentDeletion.Errors.Select(error => error.Message)));
        Assert.That(childAfterCascade.Success, Is.True,
            string.Join(Environment.NewLine, childAfterCascade.Errors.Select(error => error.Message)));
        Assert.That(childAfterCascade.Result, Is.Null);
    }

    private static async Task VerifyManyToManyRelation<TParent, TChild, TGroup>(
        Func<TParent, TChild> createChild,
        Func<TGroup, List<TChild>> getChildren,
        Action<TGroup, List<TChild>> setChildren)
        where TParent : class, IStorable, new()
        where TChild : class, IStorable, new()
        where TGroup : class, IStorable, new()
    {
        var parentManager = GenericDM.Get<TParent>();
        var childManager = GenericDM.Get<TChild>();
        var groupManager = GenericDM.Get<TGroup>();
        var deleteGroups = await groupManager.CreateDelete<TGroup>()
            .Where(item => item.Id > 0)
            .RunWithError();
        var deleteChildren = await childManager.CreateDelete<TChild>()
            .Where(item => item.Id > 0)
            .RunWithError();
        var deleteParents = await parentManager.CreateDelete<TParent>()
            .Where(item => item.Id > 0)
            .RunWithError();
        Assert.That(deleteGroups.Success, Is.True,
            string.Join(Environment.NewLine, deleteGroups.Errors.Select(error => error.Message)));
        Assert.That(deleteChildren.Success, Is.True,
            string.Join(Environment.NewLine, deleteChildren.Errors.Select(error => error.Message)));
        Assert.That(deleteParents.Success, Is.True,
            string.Join(Environment.NewLine, deleteParents.Errors.Select(error => error.Message)));

        var parent = new TParent();
        typeof(TParent).GetProperty("Name")!.SetValue(parent, "N-N parent");
        var parentCreation = await parentManager.CreateWithError(parent);
        Assert.That(parentCreation.Success, Is.True,
            string.Join(Environment.NewLine, parentCreation.Errors.Select(error => error.Message)));

        var firstChild = createChild(parent);
        var secondChild = createChild(parent);
        typeof(TChild).GetProperty("Name")!.SetValue(firstChild, "N-N child one");
        typeof(TChild).GetProperty("Name")!.SetValue(secondChild, "N-N child two");
        var childCreation = await childManager.CreateWithError([firstChild, secondChild]);
        Assert.That(childCreation.Success, Is.True,
            string.Join(Environment.NewLine, childCreation.Errors.Select(error => error.Message)));

        var group = new TGroup();
        typeof(TGroup).GetProperty("Name")!.SetValue(group, "N-N group");
        setChildren(group, [firstChild, firstChild, secondChild]);
        var groupCreation = await groupManager.CreateWithError(group);
        var initiallyLoaded = await groupManager.CreateQuery<TGroup>()
            .Where(item => item.Id == group.Id)
            .SingleWithError();
        Assert.That(groupCreation.Success, Is.True,
            string.Join(Environment.NewLine, groupCreation.Errors.Select(error => error.Message)));
        Assert.That(initiallyLoaded.Success, Is.True,
            string.Join(Environment.NewLine, initiallyLoaded.Errors.Select(error => error.Message)));
        Assert.That(getChildren(initiallyLoaded.Result!).Select(item => item.Id),
            Is.EquivalentTo(new[] { firstChild.Id, secondChild.Id }),
            "Repeated items must create only one intermediate link.");

        setChildren(group, [secondChild]);
        var replacement = await groupManager.UpdateWithError(group);
        var afterReplacement = await groupManager.CreateQuery<TGroup>()
            .Where(item => item.Id == group.Id)
            .SingleWithError();
        Assert.That(replacement.Success, Is.True,
            string.Join(Environment.NewLine, replacement.Errors.Select(error => error.Message)));
        Assert.That(getChildren(afterReplacement.Result!).Select(item => item.Id),
            Is.EqualTo(new[] { secondChild.Id }));

        setChildren(group, []);
        var clearing = await groupManager.UpdateWithError(group);
        var afterClearing = await groupManager.CreateQuery<TGroup>()
            .Where(item => item.Id == group.Id)
            .SingleWithError();
        Assert.That(clearing.Success, Is.True,
            string.Join(Environment.NewLine, clearing.Errors.Select(error => error.Message)));
        Assert.That(getChildren(afterClearing.Result!), Is.Empty);

        setChildren(group, [firstChild]);
        var relink = await groupManager.UpdateWithError(group);
        var invalidChild = new TChild { Id = 999_999 };
        setChildren(group, [invalidChild]);
        var invalidUpdate = await groupManager.UpdateWithError(group);
        var afterInvalidUpdate = await groupManager.CreateQuery<TGroup>()
            .Where(item => item.Id == group.Id)
            .SingleWithError();
        Assert.That(relink.Success, Is.True,
            string.Join(Environment.NewLine, relink.Errors.Select(error => error.Message)));
        Assert.That(invalidUpdate.Success, Is.False);
        Assert.That(afterInvalidUpdate.Success, Is.True,
            string.Join(Environment.NewLine,
                afterInvalidUpdate.Errors.Select(error => error.Message)));
        Assert.That(getChildren(afterInvalidUpdate.Result!).Select(item => item.Id),
            Is.EqualTo(new[] { firstChild.Id }));
        Assert.That(getChildren(group), Has.Count.EqualTo(1));
        Assert.That(getChildren(group)[0], Is.SameAs(firstChild),
            "A failed N-N update must restore the caller's canonical collection.");

        var deletion = await groupManager.DeleteWithError(group);
        var firstChildAfterDeletion = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == firstChild.Id)
            .SingleWithError();
        Assert.That(deletion.Success, Is.True,
            string.Join(Environment.NewLine, deletion.Errors.Select(error => error.Message)));
        Assert.That(firstChildAfterDeletion.Success, Is.True,
            string.Join(Environment.NewLine,
                firstChildAfterDeletion.Errors.Select(error => error.Message)));
        Assert.That(firstChildAfterDeletion.Result, Is.Not.Null,
            "Deleting the N-N owner must keep linked items.");

        var invalidGroup = new TGroup();
        typeof(TGroup).GetProperty("Name")!.SetValue(invalidGroup, "Invalid N-N group");
        setChildren(invalidGroup, [invalidChild]);
        var invalidCreation = await groupManager.CreateWithError(invalidGroup);
        var invalidGroupAfterRollback = await groupManager
            .GetByIdWithError<TGroup>(invalidGroup.Id);
        Assert.That(invalidCreation.Success, Is.False);
        Assert.That(invalidGroupAfterRollback.Success, Is.False);
        Assert.That(invalidGroupAfterRollback.Result, Is.Null);
    }
}
