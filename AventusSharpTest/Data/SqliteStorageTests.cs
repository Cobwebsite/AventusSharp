using AventusSharp.Data.Storage.Sqlite;
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

    private static string ErrorMessages(IEnumerable<AventusSharp.Tools.GenericError> errors) =>
        string.Join(Environment.NewLine, errors.Select(error => error.Message));
}
