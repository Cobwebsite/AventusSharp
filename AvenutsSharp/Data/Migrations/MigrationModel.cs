using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AventusSharp.Data.Manager;
using AventusSharp.Tools;

namespace AventusSharp.Data.Migrations;

public enum MigrationModelAction { Create, Update, Delete }
public interface IMigrationModel
{
    internal MigrationModelAction? ModelAction { get; }
    internal int Priority { get; set; }
    internal string? OldName { get; set; }
    internal Type Type { get; }
    internal Dictionary<string, IMigrationProperty> Properties { get; }
    internal Task<VoidWithError> Run();
    internal ResultWithError<IMigrationProvider> GetProvider();
}
public class MigrationModel<T> : IMigrationModel where T : IStorable
{
    private MigrationModelAction? _modelAction;
    internal MigrationModelAction? ModelAction
    {
        get => _modelAction;
        set
        {
            _modelAction = value;
        }
    }
    MigrationModelAction? IMigrationModel.ModelAction
    {
        get => _modelAction;
    }
    int IMigrationModel.Priority { get; set; }
    string? IMigrationModel.OldName { get; set; }
    internal Type Type { get => typeof(T); }
    Type IMigrationModel.Type { get => Type; }

    internal Dictionary<string, IMigrationProperty> Properties = new Dictionary<string, IMigrationProperty>();
    Dictionary<string, IMigrationProperty> IMigrationModel.Properties => Properties;

    internal void ChangeModelAction(MigrationModelAction action)
    {
        if (ModelAction == null)
        {
            ModelAction = action;
        }
        else if (ModelAction == MigrationModelAction.Create)
        {
            if (action == MigrationModelAction.Delete)
            {
                ModelAction = null;
            }
        }
        else if (ModelAction == MigrationModelAction.Update)
        {
            if (action == MigrationModelAction.Delete)
            {
                ModelAction = action;
            }
        }
        else if (ModelAction == MigrationModelAction.Delete)
        {

        }
    }


    private MigrationProperty<T, U> GetOrCreateProperty<U>(string name)
    {
        if (!Properties.ContainsKey(name))
        {
            Properties[name] = new MigrationProperty<T, U>(this, name, null);
        }
        if (Properties[name] is MigrationProperty<T, U> result)
        {
            return result;
        }
        throw new Exception("Impossible");
    }
    private MigrationPropertyRef<T, U> GetOrCreatePropertyRef<U>(string name)
    {
        if (!Properties.ContainsKey(name))
        {
            Properties[name] = new MigrationPropertyRef<T, U>(this, name, null);
        }
        if (Properties[name] is MigrationPropertyRef<T, U> result)
        {
            return result;
        }
        throw new Exception("Impossible");
    }

    public MigrationProperty<T, U> AddProperty<U>(string name, MigrationPropertyOptions<U>? options = null)
    {
        var result = GetOrCreateProperty<U>(name);
        result.ChangePropertyAction(MigrationPropertyAction.Create);
        if (options != null)
        {
            result.SetOptions(options);
        }
        return result;
    }
    public MigrationProperty<T, U> AddRef<U>(string name, MigrationPropertyRefOptions<U>? options = null)
    {
        return AddRef<U, int>(name, options);
    }
    public MigrationPropertyRef<T, U> AddRef<U, Y>(string name, MigrationPropertyRefOptions<U>? options = null)
    {
        var result = GetOrCreatePropertyRef<U>(name);
        if (options == null)
        {
            options = new();
        }
        options.KeyKind = typeof(Y);
        result.SetOptions(options);
        result.ChangePropertyAction(MigrationPropertyAction.Create);
        return result;
    }

    public MigrationModel<T> AddTimestamp()
    {
        GetOrCreateProperty<DateTime>("CreatedDate").ChangePropertyAction(MigrationPropertyAction.Create);
        GetOrCreateProperty<DateTime>("UpdatedDate").ChangePropertyAction(MigrationPropertyAction.Create);
        return this;
    }

    public MigrationProperty<T, U> RemoveProperty<U>(string name)
    {
        var result = GetOrCreateProperty<U>(name);
        result.ChangePropertyAction(MigrationPropertyAction.Delete);
        return result;
    }

    public MigrationProperty<T, U> RenameProperty<U>(string name)
    {
        var result = GetOrCreateProperty<U>(name);
        // result.Name
        // result.ChangePropertyAction(MigrationPropertyAction.Update);
        return result;
    }

    public MigrationProperty<T, int> AddPrimary(string name)
    {
        return AddProperty<int>(name, new()
        {
            AutoIncrement = true,
            Primary = true,
        });
    }

    private async Task<VoidWithError> _Run()
    {
        ResultWithError<IMigrationProvider> providerQuery = _GetProvider();
        if (!providerQuery.Success || providerQuery.Result == null)
        {
            VoidWithError result = new()
            {
                Errors = providerQuery.Errors
            };
            return result;
        }
        return await providerQuery.Result.ApplyMigration<T>(this);
    }

    private ResultWithError<IMigrationProvider> _GetProvider()
    {
        ResultWithError<IMigrationProvider> result = new();
        ResultWithError<IGenericDM> DMWithError = GenericDM.GetWithError<T>();
        if (DMWithError.Success && DMWithError.Result != null)
        {
            result.Result = DMWithError.Result.GetMigrationProvider();
        }
        else
        {
            result.Errors = DMWithError.Errors;
        }
        return result;
    }

    ResultWithError<IMigrationProvider> IMigrationModel.GetProvider()
    {
        return _GetProvider();
    }
    async Task<VoidWithError> IMigrationModel.Run()
    {
        return await _Run();
    }
}
