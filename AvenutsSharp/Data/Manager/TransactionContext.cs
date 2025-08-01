
using System;
using AventusSharp.Tools;

namespace AventusSharp.Data.Manager;


public abstract class TransactionContext : IDisposable
{


    private bool isEnded = false;


    public int count;
    private Action<Action> _runInsideLocker;
    private Action _endTransaction;

    public TransactionContext(Action endTransaction, Action<Action> runInsideLocker)
    {
        _endTransaction = endTransaction;
        _runInsideLocker = runInsideLocker;
        count = 1;
    }

    public ResultWithError<bool> Commit()
    {
        ResultWithError<bool> result = new ResultWithError<bool>();
        _runInsideLocker(() =>
        {
            if (isEnded)
            {
                result.Result = false;
                return;
            }
            count--;
            if (count <= 0)
            {
                isEnded = true;
                result = _Commit();
                return;
            }
            result.Result = false;
        });
        return result;
    }

    private ResultWithError<bool> _Commit()
    {
        ResultWithError<bool> result = new();
        try
        {
            TransactionCommit();
            result.Result = true;
            _endTransaction();
        }
        catch (Exception e)
        {
            result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
        }
        return result;
    }


    public ResultWithError<bool> Rollback()
    {
        ResultWithError<bool> result = new ResultWithError<bool>();
        _runInsideLocker(() =>
        {
            if (isEnded)
            {
                result.Result = false;
                return;
            }
            isEnded = true;
            result = _Rollback();
        });
        return result;
    }

    private ResultWithError<bool> _Rollback()
    {
        ResultWithError<bool> result = new();
        try
        {
            TransactionRollback();
            result.Result = true;
            _endTransaction();
        }
        catch (Exception e)
        {
            result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
        }
        return result;
    }

    public void Dispose()
    {
        _Rollback();
        TransactionDispose();
    }

    protected abstract void TransactionDispose();
    protected abstract void TransactionRollback();
    protected abstract void TransactionCommit();
}
