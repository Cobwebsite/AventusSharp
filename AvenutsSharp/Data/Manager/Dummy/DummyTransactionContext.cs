using System;

namespace AventusSharp.Data.Manager.Dummy;


public class DummyTransactionContext : TransactionContext
{
    public DummyTransactionContext(Action endTransaction, Action<Action> runInsideLocker) : base(endTransaction, runInsideLocker)
    {
    }

    protected override void TransactionCommit()
    {
    }

    protected override void TransactionDispose()
    {
    }

    protected override void TransactionRollback()
    {
    }
}