using AventusSharp.Data;
using AventusSharp.Data.Storage.Sqlite;
using NUnit.Framework;

namespace AventusSharp.Data.PortableTest;

public sealed class SqlitePortableTests
{
    [Test]
    public async Task Portable_storage_opens_and_executes_a_command()
    {
        string databasePath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"portable-{Guid.NewGuid():N}.db");
        var storage = new SqliteStorage(databasePath);

        var connectionResult = await storage.ConnectWithError();
        Assert.That(connectionResult.Success, Is.True);

        await using var connection = storage.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        object? value = await command.ExecuteScalarAsync();

        Assert.That(Convert.ToInt32(value), Is.EqualTo(1));
    }

    [Test]
    public void Portable_data_exposes_Storable_without_server_drivers()
    {
        Assert.That(typeof(PortableRecord).BaseType, Is.Not.Null);

        string dataProject = FindProject("AventusSharp.Data");
        string sqliteProject = FindProject("AventusSharp.Data.Sqlite");
        string projectGraph = File.ReadAllText(dataProject) + File.ReadAllText(sqliteProject);

        Assert.Multiple(() =>
        {
            Assert.That(projectGraph, Does.Not.Contain("Microsoft.AspNetCore.App"));
            Assert.That(projectGraph, Does.Not.Contain("Microsoft.Data.SqlClient"));
            Assert.That(projectGraph, Does.Not.Contain("MySql.Data"));
            Assert.That(projectGraph, Does.Not.Contain("Npgsql"));
        });
    }

    private static string FindProject(string projectName)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, projectName, projectName + ".csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException(projectName);
    }

    private sealed class PortableRecord : Storable<PortableRecord>
    {
        public string Name { get; set; } = string.Empty;
    }
}
