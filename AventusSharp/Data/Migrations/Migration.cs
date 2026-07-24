

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;

namespace AventusSharp.Data.Migrations;

public interface IMigration
{
}
public abstract class Migration : IMigration
{
    private VoidWithError _currentError = new VoidWithError();
    public abstract string GetName();

    private int priority = 0;
    private Dictionary<string, IMigrationModel> models = new Dictionary<string, IMigrationModel>();

    public async Task<VoidWithError> _Up(List<IMigrationProvider> providers)
    {
        _currentError = new VoidWithError();
        string name = GetName();
        if (providers.Count == 1)
        {
            ResultWithError<bool> canExectue = await providers[0].Can(name);
            if (!canExectue.Success || !canExectue.Result)
            {
                _currentError.Errors = canExectue.Errors;
                return _currentError;
            }
        }

        Up();

        List<IMigrationModel> migrations = models.Values.ToList();
        migrations.Sort((a, b) => a.Priority - b.Priority);
        if (providers.Count > 1)
        {
            providers = new();
            foreach (IMigrationModel migration in migrations)
            {
                ResultWithError<IMigrationProvider> providerQuery = migration.GetProvider();
                if (providerQuery.Success && providerQuery.Result != null)
                {
                    if (!providers.Contains(providerQuery.Result))
                    {
                        providers.Add(providerQuery.Result);
                    }
                }
                else
                {
                    _currentError.Errors = providerQuery.Errors;
                    return _currentError;
                }
            }

            ResultWithError<bool> canExectue = new();
            foreach (IMigrationProvider provider in providers)
            {
                await canExectue.RunAsync(async () => await provider.Can(name));
            }
            if (!canExectue.Success || !canExectue.Result)
            {
                _currentError.Errors = canExectue.Errors;
                return _currentError;
            }
        }

        foreach (IMigrationProvider provider in providers)
        {
            await provider.BeforeUp(_currentError);
            if (provider is IStorageMigrationProvider stProvider)
            {
                ResultWithError<DbTransactionContext> transactionQuery = await stProvider.BeginTransaction();
                if (transactionQuery.Success && transactionQuery.Result != null)
                {
                    stProvider.setTransactionScope(transactionQuery.Result);
                }
            }
        }

        foreach (IMigrationModel migration in migrations)
        {
            await _currentError.RunAsync(migration.Run);
        }

        foreach (IMigrationProvider provider in providers)
        {
            await provider.AfterUp(_currentError);
            if (provider is IStorageMigrationProvider stProvider)
            {
                stProvider.setTransactionScope(null);
            }
        }

        if (_currentError.Success)
        {
            foreach (IMigrationProvider provider in providers)
            {
                await _currentError.RunAsync(() => provider.Save(name));
            }
        }

        return _currentError;
    }
    public abstract void Up();

    public VoidWithError _Down()
    {
        _currentError = new VoidWithError();
        Down();
        return _currentError;
    }
    public abstract void Down();

    private MigrationModel<T> GetOrCreateModel<T>() where T : IStorable
    {
        string fullName = typeof(T).FullName ?? "";
        if (!models.ContainsKey(fullName))
        {
            priority++;
            models[fullName] = new MigrationModel<T>();
            models[fullName].Priority = priority;
        }
        if (models[fullName] is MigrationModel<T> result)
        {
            return result;
        }
        throw new Exception("Impossible");
    }
    public MigrationModel<T> CreateModel<T>() where T : IStorable
    {
        var result = GetOrCreateModel<T>();
        result.ChangeModelAction(MigrationModelAction.Create);
        return result;
    }
    public MigrationModel<T> RenameModel<T>(string oldName) where T : IStorable
    {
        var result = GetOrCreateModel<T>();
        result.ChangeModelAction(MigrationModelAction.Update);
        ((IMigrationModel)result).OldName = oldName;
        return result;
    }
    public void DeleteModel<T>() where T : IStorable
    {
        var result = GetOrCreateModel<T>();
        result.ChangeModelAction(MigrationModelAction.Delete);
    }
    public MigrationModel<T> SelectModel<T>() where T : IStorable
    {
        var result = GetOrCreateModel<T>();
        return result;
    }
}

