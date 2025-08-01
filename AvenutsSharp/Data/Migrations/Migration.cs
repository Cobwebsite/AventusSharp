

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using SQLitePCL;

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

    public VoidWithError _Up(List<IMigrationProvider> providers)
    {
        _currentError = new VoidWithError();
        string name = GetName();
        if (providers.Count == 1)
        {
            ResultWithError<bool> canExectue = providers[0].Can(name);
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
                canExectue.Execute(() => provider.Can(name));
            }
            if (!canExectue.Success || !canExectue.Result)
            {
                _currentError.Errors = canExectue.Errors;
                return _currentError;
            }
        }

        foreach (IMigrationProvider provider in providers)
        {
            provider.BeforeUp(_currentError);
        }

        foreach (IMigrationModel migration in migrations)
        {
            _currentError.Run(migration.Run);
        }

        foreach (IMigrationProvider provider in providers)
        {
            provider.AfterUp(_currentError);
        }

        if (_currentError.Success)
        {
            foreach (IMigrationProvider provider in providers)
            {
                _currentError.Run(() => provider.Save(name));
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

public enum MigrationModelAction { Create, Update, Delete }
public interface IMigrationModel
{
    internal MigrationModelAction? ModelAction { get; }
    internal int Priority { get; set; }
    internal string? OldName { get; set; }
    internal Type Type { get; }
    internal Dictionary<string, IMigrationProperty> Properties { get; }
    internal VoidWithError Run();
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

    public MigrationProperty<T, U> RemoveProperty<U>(string name)
    {
        var result = GetOrCreateProperty<U>(name);
        result.ChangePropertyAction(MigrationPropertyAction.Delete);
        return result;
    }

    // public MigrationProperty<T, U> RenameProperty<U>(string name)
    // {
    //     var result = new MigrationProperty<T, U>(this, name, null);
    //     return result;
    // }

    public MigrationProperty<T, int> AddPrimary(string name)
    {
        return AddProperty<int>(name, new()
        {
            AutoIncrement = true,
            Primary = true,
        });
    }

    private VoidWithError _Run()
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
        return providerQuery.Result.ApplyMigration<T>(this);
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
    VoidWithError IMigrationModel.Run()
    {
        return _Run();
    }
}

public class MigrationPropertyOptions<T>
{
    public bool AutoIncrement { get; set; }
    public bool Unique { get; set; }
    public bool Primary { get; set; }
    public bool Nullable { get; set; }
    public bool Index { get; set; }
    public T? Default { get; set; }
}
public enum MigrationPropertyAction { Create, Update, Delete }
public interface IMigrationProperty { }
public class MigrationProperty<T, U> : IMigrationProperty where T : IStorable
{
    private string Name { get; set; }
    private MigrationModel<T> Table { get; set; }
    private MigrationPropertyOptions<U> Options { get; set; }

    private MigrationPropertyAction? PropertyAction { get; set; }


    public MigrationProperty(MigrationModel<T> table, MigrationPropertyOptions<U>? options)
    {
        Table = table;
        Name = typeof(T).Name; // Get type
        Options = options ?? new();
    }
    public MigrationProperty(MigrationModel<T> table, string name, MigrationPropertyOptions<U>? options)
    {
        Table = table;
        Name = name;
        Options = options ?? new();
    }

    internal void SetOptions(MigrationPropertyOptions<U> options)
    {
        Options = options;
    }
    internal void ChangePropertyAction(MigrationPropertyAction action)
    {
        if (PropertyAction == null)
        {
            PropertyAction = action;
        }
        else if (PropertyAction == MigrationPropertyAction.Create)
        {
            if (action == MigrationPropertyAction.Delete)
            {
                PropertyAction = null;
            }
        }
        else if (PropertyAction == MigrationPropertyAction.Update)
        {
            if (action == MigrationPropertyAction.Delete)
            {
                PropertyAction = action;
            }
        }
        else if (PropertyAction == MigrationPropertyAction.Delete)
        {

        }
    }


    public MigrationProperty<T, X> AddProperty<X>(string name, MigrationPropertyOptions<X>? options = null)
    {
        return Table.AddProperty(name, options);
    }
    public MigrationProperty<T, int> AddPrimary<X>(string name)
    {
        return Table.AddPrimary(name);
    }

}