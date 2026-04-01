using System;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager.Dummy;


public class DummyTransactionContext : TransactionContext
{
    public DummyTransactionContext(Func<Task> endTransaction) : base(endTransaction)
    {
    }

    protected override Task TransactionCommit()
    {
        return Task.CompletedTask;
    }

    protected override Task TransactionDispose()
    {
        return Task.CompletedTask;
    }

    protected override Task TransactionRollback()
    {
        return Task.CompletedTask;
    }
}