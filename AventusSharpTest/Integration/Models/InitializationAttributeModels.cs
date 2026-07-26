using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Sqlite;

namespace AventusSharpTest.Integration.Models;

public sealed class DedicatedTestStorage : SqliteStorage
{
    public static string DatabasePath { get; } = Path.Combine(
        AppContext.BaseDirectory,
        "aventus-dedicated-storage-tests.db");

    public static DedicatedTestStorage? Instance { get; private set; }

    public DedicatedTestStorage() : base(DatabasePath)
    {
        Instance = this;
    }
}

[Storage<DedicatedTestStorage>]
[SqlName("dedicated_storage_records")]
public sealed class DedicatedStorageRecord : Storable<DedicatedStorageRecord>
{
    public string Name { get; set; } = "";
}

public sealed class DedicatedStorageRecordManager
    : DatabaseDM<DedicatedStorageRecordManager, DedicatedStorageRecord>
{
}

[ManualInit]
[SqlName("manual_init_records")]
public sealed class ManualInitRecord : Storable<ManualInitRecord>
{
    public string Name { get; set; } = "";
}
