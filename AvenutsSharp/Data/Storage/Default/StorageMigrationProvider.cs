using System;
using AventusSharp.Data.Migrations;
using AventusSharp.Tools;

namespace AventusSharp.Data.Storage.Default;

public abstract class StorageMigrationProvider<T> : MigrationProvider where T : DefaultDBStorage<T>
{
    private T _storage;
    public StorageMigrationProvider(T storage)
    {
        _storage = storage;
    }
    public override VoidWithError Init()
    {
        VoidWithError result = new();
        TableInfo tableInfo = new TableInfo(typeof(MigrationTable));
        tableInfo.Init();
        result.Run(() => _storage.CreateTable(tableInfo));
        return result;
    }
}

internal class MigrationTable : Storable<MigrationTable>
{
    public required string Name { get; set; }
    public required DateTime Date { get; set; }
}
