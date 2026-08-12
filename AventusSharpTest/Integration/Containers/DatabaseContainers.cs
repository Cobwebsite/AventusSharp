using System.Diagnostics;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Mssql;
using AventusSharp.Data.Storage.Mysql;
using AventusSharp.Data.Storage.Postgresql;
using AventusSharp.Data.Storage.Sqlite;
using NUnit.Framework;

namespace AventusSharpTest.Integration.Containers;

internal static class DatabaseContainers
{
    private const string Database = "aventus_tests";
    private const string User = "aventus";
    private const string Password = "aventus_test_password";

    private static readonly string ComposeFile = Path.Combine(
        FindProjectDirectory(),
        "docker-compose.databases.yml");

    internal static bool Available { get; private set; }
    internal static string? UnavailableReason { get; private set; }

    internal static AventusSharp.Data.Storage.Default.IDBStorage MySql { get; private set; } = null!;
    internal static AventusSharp.Data.Storage.Default.IDBStorage PostgreSql { get; private set; } = null!;
    internal static AventusSharp.Data.Storage.Default.IDBStorage MsSql { get; private set; } = null!;

    internal static async Task StartOrCreateFallbacks(string workingDirectory)
    {
        var dockerVersion = await RunDocker("version", "--format", "{{.Server.Version}}");
        if (dockerVersion.ExitCode != 0)
        {
            UnavailableReason = string.IsNullOrWhiteSpace(dockerVersion.Error)
                ? "Docker is not installed or the daemon is unavailable."
                : dockerVersion.Error.Trim();
            CreateFallbacks(workingDirectory);
            return;
        }

        var up = await RunDocker(
            "compose", "-f", ComposeFile, "up", "-d", "--wait", "--remove-orphans");
        if (up.ExitCode != 0)
        {
            UnavailableReason = $"docker compose up failed: {up.Error}";
            CreateFallbacks(workingDirectory);
            return;
        }

        try
        {
            var mysqlPort = await GetPort("mysql", 3306);
            var postgresPort = await GetPort("postgresql", 5432);
            var msSqlPort = await GetPort("mssql", 1433);

            MySql = new MySQLStorage(new StorageCredentials()
            {
                Host = "127.0.0.1",
                Port = mysqlPort,
                Username = User,
                Password = Password,
                Database = Database
            });
            PostgreSql = new PostgreSqlStorage(new StorageCredentials()
            {
                Host = "127.0.0.1",
                Port = postgresPort,
                Username = User,
                Password = Password,
                Database = Database
            });
            MsSql = new MsSqlStorage(new StorageCredentials()
            {
                Host = "127.0.0.1",
                Port = msSqlPort,
                Username = "sa",
                Password = "Aventus_Test_123!",
                Database = "master",
                TrustServerCertificate = true
            });

            await WaitUntilConnectionsAreStable();
            Available = true;
        }
        catch (Exception exception)
        {
            UnavailableReason = exception.Message;
            await Stop();
            CreateFallbacks(workingDirectory);
        }
    }

    internal static async Task Stop()
    {
        if (!Available)
            return;

        await RunDocker("compose", "-f", ComposeFile, "down", "-v", "--remove-orphans");
        Available = false;
    }

    internal static void RequireDocker()
    {
        if (Available)
            return;

        var reason = UnavailableReason ?? "Docker is unavailable.";
        if (Environment.GetEnvironmentVariable("AVENTUS_REQUIRE_DOCKER") == "1")
            Assert.Fail(reason);

        Assert.Ignore(reason);
    }

    private static void CreateFallbacks(string workingDirectory)
    {
        MySql = new SqliteStorage(Path.Combine(workingDirectory, "mysql-fallback.db"));
        PostgreSql = new SqliteStorage(Path.Combine(workingDirectory, "postgresql-fallback.db"));
        MsSql = new SqliteStorage(Path.Combine(workingDirectory, "mssql-fallback.db"));
    }

    private static async Task WaitUntilConnectionsAreStable()
    {
        foreach (var (name, storage) in new[]
                 {
                     ("MySQL", MySql),
                     ("PostgreSQL", PostgreSql),
                     ("SQL Server", MsSql)
                 })
        {
            var consecutiveSuccesses = 0;
            string lastError = "";
            for (var attempt = 0; attempt < 20 && consecutiveSuccesses < 2; attempt++)
            {
                var connection = await storage.ConnectWithError();
                if (connection.Success)
                {
                    consecutiveSuccesses++;
                }
                else
                {
                    consecutiveSuccesses = 0;
                    lastError = string.Join(Environment.NewLine,
                        connection.Errors.Select(error => error.GetMessageException(true)));
                }

                if (consecutiveSuccesses < 2)
                    await Task.Delay(500);
            }

            if (consecutiveSuccesses < 2)
                throw new InvalidOperationException($"{name} did not become stable.{Environment.NewLine}{lastError}");
        }
    }

    private static async Task<uint> GetPort(string service, int containerPort)
    {
        var result = await RunDocker(
            "compose", "-f", ComposeFile, "port", service, containerPort.ToString());
        if (result.ExitCode != 0)
            throw new InvalidOperationException(result.Error);

        var endpoint = result.Output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        var portText = endpoint[(endpoint.LastIndexOf(':') + 1)..].Trim();
        return uint.Parse(portText);
    }

    private static async Task<ProcessResult> RunDocker(params string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            foreach (var argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
        }
        catch (Exception exception)
        {
            return new ProcessResult(-1, "", exception.Message);
        }
    }

    private static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "docker-compose.databases.yml");
            if (File.Exists(candidate))
                return directory.FullName;

            candidate = Path.Combine(directory.FullName, "AventusSharpTest", "docker-compose.databases.yml");
            if (File.Exists(candidate))
                return Path.GetDirectoryName(candidate)!;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("docker-compose.databases.yml was not found.");
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
