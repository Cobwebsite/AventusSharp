
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AventusSharp.Tools;

namespace AventusSharp.Data.Manager;


public abstract class TransactionContext : IAsyncDisposable, IDisposable
{


    private bool isEnded = false;


    public int count;
    private Func<Task> _endTransaction;
    private readonly List<Func<Task<VoidWithError>>> _rollbackActions = [];

    public TransactionContext(Func<Task> endTransaction)
    {
        _endTransaction = endTransaction;
        count = 1;
    }

    public async Task<ResultWithError<bool>> Commit()
    {
        ResultWithError<bool> result = new ResultWithError<bool>();

        if (isEnded)
        {
            result.Result = false;
            return result;
        }
        count--;
        if (count <= 0)
        {
            isEnded = true;
            return await _Commit();
        }
        result.Result = false;
        return result;
    }

    private async Task<ResultWithError<bool>> _Commit()
    {
        ResultWithError<bool> result = new();
        try
        {
            await TransactionCommit();
            result.Result = true;
            _rollbackActions.Clear();
            await _endTransaction();
        }
        catch (Exception e)
        {
            result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
        }
        return result;
    }


    public async Task<ResultWithError<bool>> Rollback()
    {
        ResultWithError<bool> result = new ResultWithError<bool>();

        if (isEnded)
        {
            result.Result = false;
            return result;
        }
        isEnded = true;
        result = await _Rollback();
        return result;
    }

    private async Task<ResultWithError<bool>> _Rollback()
    {
        ResultWithError<bool> result = new();
        try
        {
            await TransactionRollback();
            VoidWithError rollbackActionsResult = new();
            for (int index = _rollbackActions.Count - 1; index >= 0; index--)
            {
                await rollbackActionsResult.RunAsync(_rollbackActions[index]);
            }
            _rollbackActions.Clear();
            result.Errors.AddRange(rollbackActionsResult.Errors);
            result.Result = rollbackActionsResult.Success;
            await _endTransaction();
        }
        catch (Exception e)
        {
            result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
        }
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (!isEnded)
        {
            isEnded = true;
            await _Rollback();
        }
        await TransactionDispose();
    }
    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    protected abstract Task TransactionDispose();
    protected abstract Task TransactionRollback();
    protected abstract Task TransactionCommit();

    public void OnRollback(Func<Task<VoidWithError>> action)
    {
        _rollbackActions.Add(action);
    }

    
}
