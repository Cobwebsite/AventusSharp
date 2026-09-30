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

    [ReverseLink(nameof(CrossStorageOwner.Record))]
    public List<CrossStorageOwner> Owners { get; set; } = [];
}

public sealed class DedicatedStorageRecordManager
    : DatabaseDM<DedicatedStorageRecordManager, DedicatedStorageRecord>
{
}

[SqlName("cross_storage_owners")]
public sealed class CrossStorageOwner : Storable<CrossStorageOwner>
{
    public string Name { get; set; } = "";

    [AutoRead]
    public DedicatedStorageRecord? Record { get; set; }

    [AutoRead]
    public List<DedicatedStorageRecord> Records { get; set; } = [];
}

public sealed class CrossStorageOwnerManager
    : DatabaseDM<CrossStorageOwnerManager, CrossStorageOwner>
{
}

[ManualInit]
[SqlName("manual_init_records")]
public sealed class ManualInitRecord : Storable<ManualInitRecord>
{
    public string Name { get; set; } = "";
}
