using System;
using System.Diagnostics;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Migrations;
using AventusSharp.Tools;

namespace AventusSharp.Data.Storage.Default;

public abstract class StorageMigrationProvider<T> : MigrationProvider where T : DefaultDBStorage<T>
{
    private T _storage;
    private TransactionContext? _context;
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

    public override ResultWithError<bool> Can(string name)
    {
        ResultWithError<bool> result = MigrationTable.ExistWithError(p => p.Name == name);
        result.Result = !result.Result;
        return result;
    }

    public override VoidWithError Save(string name)
    {
        MigrationTable migration = new()
        {
            Date = DateTime.Now,
            Name = name
        };
        VoidWithError result = new()
        {
            Errors = migration.CreateWithError()
        };
        return result;
    }

    public override void BeforeUp(VoidWithError voidWithError)
    {
        ResultWithError<TransactionContext> transactionQuery = _storage.BeginTransaction();
        if (transactionQuery.Success && transactionQuery.Result != null)
        {
            _context = transactionQuery.Result;
        }
        voidWithError.Errors.AddRange(transactionQuery.Errors);
    }

    public override void AfterUp(VoidWithError voidWithError)
    {
        if (_context != null)
        {
            if (voidWithError.Success)
            {
                ResultWithError<bool> commitQuery = _context.Commit();
                voidWithError.Errors.AddRange(commitQuery.Errors);
            }
            else
            {
                ResultWithError<bool> rollbackQuery = _context.Rollback();
                voidWithError.Errors.AddRange(rollbackQuery.Errors);
            }
        }
    }
}

[ManualInit]
internal class MigrationTable : Storable<MigrationTable>
{
    public required string Name { get; set; }
    public required DateTime Date { get; set; }
}
