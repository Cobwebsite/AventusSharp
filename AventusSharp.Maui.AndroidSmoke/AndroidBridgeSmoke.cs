using AventusSharp.Hosting;
using AventusSharp.Maui;
using AventusSharp.Data;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Sqlite;
using AventusSharp.Tools;

namespace AventusSharp.Maui.AndroidSmoke;

public static class AndroidBridgeSmoke
{
    public static AventusMauiBridge Create(
        IAventusRequestDispatcher dispatcher,
        IServiceProvider services)
    {
        return new AventusMauiBridge(dispatcher, () => services);
    }

    public static SqliteStorage CreateLocalStorage(string databasePath) => new(databasePath);

    public static Task<VoidWithError> InitializeData(string databasePath)
    {
        var storage = CreateLocalStorage(databasePath);
        DataMainManager.Configure(config =>
        {
            config.defaultStorage = storage;
            config.defaultDM = typeof(SimpleDatabaseDM<>);
            config.AutoCreateModel = true;
        });

        return DataMainManager.Init(typeof(AndroidSmokeRecord).Assembly);
    }
}

public sealed class AndroidSmokeRecord : Storable<AndroidSmokeRecord>
{
    public string Name { get; set; } = string.Empty;
}
