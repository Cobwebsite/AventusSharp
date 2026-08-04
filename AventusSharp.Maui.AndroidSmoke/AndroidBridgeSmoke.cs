using AventusSharp.Hosting;
using AventusSharp.Maui;
using AventusSharp.Data;
using AventusSharp.Data.Storage.Sqlite;

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
}

public sealed class AndroidSmokeRecord : Storable<AndroidSmokeRecord>
{
    public string Name { get; set; } = string.Empty;
}
