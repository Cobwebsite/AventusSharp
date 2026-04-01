using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Migrations;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;

namespace AventusSharp.Data.Storage.Default;

public abstract class StorageMigrationProvider<T> : MigrationProvider where T : DefaultDBStorage<T>
{
    private T _storage;
    private DbTransactionContext? _context;
    public StorageMigrationProvider(T storage)
    {
        _storage = storage;
    }
    public override async Task<VoidWithError> Init()
    {
        VoidWithError result = new();
        TableInfo tableInfo = new TableInfo(typeof(MigrationTable));
        tableInfo.Init();
        await result.RunAsync(() => _storage.CreateTable(tableInfo));
        InitMigrationTableDM();
        return result;
    }

    public override async Task<ResultWithError<bool>> Can(string name)
    {
        ResultWithError<bool> result = await MigrationTable.ExistWithError(p => p.Name == name);
        result.Result = !result.Result;
        return result;
    }

    public override async Task<VoidWithError> Save(string name)
    {
        MigrationTable migration = new()
        {
            Date = DateTime.Now,
            Name = name
        };
        VoidWithError result = new()
        {
            Errors = await migration.CreateWithError()
        };
        return result;
    }

    public override async Task BeforeUp(VoidWithError voidWithError)
    {
        ResultWithError<DbTransactionContext> transactionQuery = await _storage.BeginTransaction();
        if (transactionQuery.Success && transactionQuery.Result != null)
        {
            _context = transactionQuery.Result;
        }
        voidWithError.Errors.AddRange(transactionQuery.Errors);
    }

    public override async Task AfterUp(VoidWithError voidWithError)
    {
        if (_context != null)
        {
            if (voidWithError.Success)
            {
                ResultWithError<bool> commitQuery = await _context.Commit();
                voidWithError.Errors.AddRange(commitQuery.Errors);
            }
            else
            {
                ResultWithError<bool> rollbackQuery = await _context.Rollback();
                voidWithError.Errors.AddRange(rollbackQuery.Errors);
            }
        }
    }

    public override Task<VoidWithError> ApplyMigration<X>(IMigrationModel model)
    {
        return _storage.ApplyMigration<X>(model);
    }
}

[ManualInit]
[NoExport]
internal class MigrationTable : Storable<MigrationTable>
{
    public required string Name { get; set; }
    public required DateTime Date { get; set; }
}
