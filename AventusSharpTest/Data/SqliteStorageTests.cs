using AventusSharp.Data.Storage.Sqlite;
using AventusSharp.Data;
using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Data;

[TestFixture]
[NonParallelizable]
public class SqliteStorageTests
{
    private string _databasePath = null!;
    private SqliteStorage _storage = null!;

    [SetUp]
    public async Task SetUp()
    {
        _databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "aventus-integration-tests.db");
        _storage = new SqliteStorage(_databasePath);
        var connected = await _storage.ConnectWithError();
        Assert.That(connected.Success, Is.True, ErrorMessages(connected.Errors));
        var reset = await _storage.ResetStorage();
        Assert.That(reset.Success, Is.True, ErrorMessages(reset.Errors));
    }

    [Test]
    public async Task Execute_and_query_round_trip_values()
    {
        var create = await _storage.Execute(
            "CREATE TABLE sample (id INTEGER PRIMARY KEY, name TEXT NOT NULL);" +
            "INSERT INTO sample (name) VALUES ('Aventus');");
        Assert.That(create.Success, Is.True, ErrorMessages(create.Errors));

        var query = await _storage.Query("SELECT id, name FROM sample;");

        Assert.That(query.Success, Is.True, ErrorMessages(query.Errors));
        Assert.That(query.Result, Has.Count.EqualTo(1));
        Assert.That(query.Result![0]["name"], Is.EqualTo("Aventus"));
    }

    [Test]
    public async Task IsConnected_succeeds_when_sqlite_is_reachable()
    {
        var result = await _storage.IsConnected();

        Assert.That(result.Success, Is.True, ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Reset_storage_removes_user_tables()
    {
        var create = await _storage.Execute("CREATE TABLE disposable (id INTEGER PRIMARY KEY);");
        Assert.That(create.Success, Is.True, ErrorMessages(create.Errors));

        var reset = await _storage.ResetStorage();
        var query = await _storage.Query(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='disposable';");

        Assert.That(reset.Success, Is.True, ErrorMessages(reset.Errors));
        Assert.That(query.Result, Is.Empty);
    }

    [Test]
    public async Task Read_only_storage_allows_queries_and_rejects_writes()
    {
        var create = await _storage.Execute(
            "CREATE TABLE read_only_sample (id INTEGER PRIMARY KEY, name TEXT NOT NULL);" +
            "INSERT INTO read_only_sample (id, name) VALUES (1, 'kept');");
        Assert.That(create.Success, Is.True, ErrorMessages(create.Errors));

        _storage.ReadOnly = true;
        var query = await _storage.Query("SELECT name FROM read_only_sample WHERE id = 1;");
        var update = await _storage.Execute(
            "UPDATE read_only_sample SET name = 'changed' WHERE id = 1;");

        Assert.Multiple(() =>
        {
            Assert.That(query.Success, Is.True, ErrorMessages(query.Errors));
            Assert.That(query.Result![0]["name"], Is.EqualTo("kept"));
            Assert.That(update.Success, Is.False);
            Assert.That(update.Errors.OfType<AventusSharp.Data.DataError>()
                .Select(error => error.Code), Does.Contain(AventusSharp.Data.DataErrorCode.IsReadOnly));
        });
    }

    [Test]
    public async Task QueryStream_preserves_nulls_and_visits_rows_in_order()
    {
        var create = await _storage.Execute(
            "CREATE TABLE stream_sample (id INTEGER PRIMARY KEY, value TEXT NULL);" +
            "INSERT INTO stream_sample (id, value) VALUES (1, 'first'), (2, NULL), (3, 'last');");
        Assert.That(create.Success, Is.True, ErrorMessages(create.Errors));
        var visited = new List<Dictionary<string, string?>>();

        var result = await _storage.QueryStream(
            "SELECT id, value FROM stream_sample ORDER BY id;",
            row =>
            {
                visited.Add(row);
                return Task.FromResult(new VoidWithError());
            });

        Assert.That(result.Success, Is.True, ErrorMessages(result.Errors));
        Assert.That(visited.Select(row => row["id"]), Is.EqualTo(new[] { "1", "2", "3" }));
        Assert.That(visited[0]["value"], Is.EqualTo("first"));
        Assert.That(visited[1]["value"], Is.Null);
        Assert.That(visited[2]["value"], Is.EqualTo("last"));
    }

    [Test]
    public async Task QueryStream_propagates_callback_errors_and_stops_invoking_it()
    {
        var create = await _storage.Execute(
            "CREATE TABLE callback_sample (id INTEGER PRIMARY KEY);" +
            "INSERT INTO callback_sample (id) VALUES (1), (2), (3);");
        Assert.That(create.Success, Is.True, ErrorMessages(create.Errors));
        var visited = new List<string?>();

        var result = await _storage.QueryStream(
            "SELECT id FROM callback_sample ORDER BY id;",
            row =>
            {
                visited.Add(row["id"]);
                var callbackResult = new VoidWithError();
                if (row["id"] == "2")
                {
                    callbackResult.Errors.Add(new DataError(
                        DataErrorCode.ValidationError,
                        "Stop streaming"));
                }
                return Task.FromResult(callbackResult);
            });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.OfType<DataError>().Select(error => error.Code),
            Does.Contain(DataErrorCode.ValidationError));
        Assert.That(visited, Is.EqualTo(new[] { "1", "2" }));
    }

    [Test]
    public async Task Invalid_sql_is_returned_as_a_monadic_error_with_query_context()
    {
        const string sql = "SELECT missing_column FROM missing_table;";

        var result = await _storage.Query(sql);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors, Is.Not.Empty);
        Assert.That(result.Errors.SelectMany(error => error.Details),
            Does.Contain(sql));
    }

    [Test]
    public async Task Invalid_sql_does_not_poison_the_next_command_or_connection()
    {
        var invalid = await _storage.Execute(
            "INSERT INTO a_table_that_does_not_exist (value) VALUES (1);");
        var create = await _storage.Execute(
            "CREATE TABLE recovery_sample (id INTEGER PRIMARY KEY, value TEXT);" +
            "INSERT INTO recovery_sample (id, value) VALUES (1, 'recovered');");
        var query = await _storage.Query(
            "SELECT value FROM recovery_sample WHERE id = 1;");

        Assert.Multiple(() =>
        {
            Assert.That(invalid.Success, Is.False);
            Assert.That(invalid.Errors, Is.Not.Empty);
            Assert.That(create.Success, Is.True, ErrorMessages(create.Errors));
            Assert.That(query.Success, Is.True, ErrorMessages(query.Errors));
            Assert.That(query.Result!.Single()["value"], Is.EqualTo("recovered"));
        });
    }

    [Test]
    public async Task QueryStream_callback_exception_is_monadic_and_releases_the_connection()
    {
        var create = await _storage.Execute(
            "CREATE TABLE throwing_stream_sample (id INTEGER PRIMARY KEY);" +
            "INSERT INTO throwing_stream_sample (id) VALUES (1), (2), (3);");
        Assert.That(create.Success, Is.True, ErrorMessages(create.Errors));
        var visited = new List<string?>();

        var stream = await _storage.QueryStream(
            "SELECT id FROM throwing_stream_sample ORDER BY id;",
            row =>
            {
                visited.Add(row["id"]);
                if (row["id"] == "2")
                    throw new InvalidOperationException("stream callback exception");
                return Task.FromResult(new VoidWithError());
            });
        var afterFailure = await _storage.Query(
            "SELECT COUNT(*) AS count FROM throwing_stream_sample;");

        Assert.Multiple(() =>
        {
            Assert.That(stream.Success, Is.False);
            Assert.That(ErrorMessages(stream.Errors),
                Does.Contain("stream callback exception"));
            Assert.That(visited, Is.EqualTo(new[] { "1", "2" }));
            Assert.That(afterFailure.Success, Is.True,
                ErrorMessages(afterFailure.Errors));
            Assert.That(afterFailure.Result!.Single()["count"], Is.EqualTo("3"));
        });
    }

    private static string ErrorMessages(IEnumerable<AventusSharp.Tools.GenericError> errors) =>
        string.Join(Environment.NewLine, errors.Select(error => error.Message));
}
