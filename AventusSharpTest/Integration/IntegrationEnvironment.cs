using AventusSharp.Data;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Sqlite;
using AventusSharpTest.Integration.Containers;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[SetUpFixture]
public sealed class IntegrationEnvironment
{
    internal static SqliteStorage Storage { get; private set; } = null!;
    internal static string DatabasePath { get; private set; } = "";

    [OneTimeSetUp]
    public async Task InitializeAventusData()
    {
        DatabasePath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "aventus-model-integration-tests.db");

        Storage = new SqliteStorage(DatabasePath);
        await DatabaseContainers.StartOrCreateFallbacks(TestContext.CurrentContext.WorkDirectory);
        var reset = await Storage.ResetStorage();
        Assert.That(reset.Success, Is.True, ErrorMessages(reset.Errors));

        DataMainManager.Configure(config =>
        {
            config.DefaultStorage = Storage;
            config.DefaultDM = typeof(SimpleDatabaseDM<>);
            config.AutoCreateModel = true;
            config.PreferLocalCache = true;
            config.PreferShortLink = false;
            config.NullByDefault = false;
        });

        var initialized = await DataMainManager.Init(typeof(IntegrationEnvironment).Assembly);
        Assert.That(initialized.Success, Is.True, ErrorMessages(initialized.Errors));
    }

    [OneTimeTearDown]
    public async Task StopDatabaseContainers()
    {
        await DatabaseContainers.Stop();
    }

    internal static string ErrorMessages(IEnumerable<AventusSharp.Tools.GenericError> errors) =>
        string.Join(Environment.NewLine, errors.Select(error => error.Message));
}
