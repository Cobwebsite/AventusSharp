using System;
using AventusSharp.Data.Attributes;

namespace AventusSharp.Data.Migrations;

public class MigrationPropertyOptions
{
    public bool AutoIncrement { get; set; }
    public bool Unique { get; set; }
    public bool Primary { get; set; }
    public bool Nullable { get; set; }
    public bool Index { get; set; }
    public object? Default { get; set; }
    public Size? Size { get; set; }
}
public class MigrationPropertyOptions<T> : MigrationPropertyOptions
{
    public new T? Default
    {
        get
        {
            if (base.Default is T t)
            {
                return t;
            }
            return default;
        }
        set
        {
            base.Default = value;
        }
    }
}
public enum MigrationPropertyAction { Create, Update, Delete }
public interface IMigrationProperty
{
    public string Name { get; }
    public string? OldName { get; }
    public Type Parent { get; }
    public Type Type { get; }
    public MigrationPropertyOptions Options { get; }
     public MigrationPropertyAction? PropertyAction { get; }
}
public class MigrationProperty<T, U> : IMigrationProperty where T : IStorable
{
    public string Name { get; private set; }
    public string? OldName { get; private set; }
    public Type Parent => typeof(T);
    public Type Type => typeof(U);
    private MigrationModel<T> Table { get; set; }
    protected MigrationPropertyOptions<U> Options { get; set; }

    public MigrationPropertyAction? PropertyAction { get; private set; }

    MigrationPropertyOptions IMigrationProperty.Options => Options;

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
    internal void SetOldName(string oldName)
    {
        OldName = oldName;
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

    public MigrationProperty<T, X> AddRef<X>(string name, MigrationPropertyRefOptions<X>? options = null)
    {
        return AddRef<X, int>(name, options);
    }
    public MigrationProperty<T, X> AddRef<X, Y>(string name, MigrationPropertyRefOptions<X>? options = null)
    {
        return Table.AddRef<X, Y>(name, options);
    }
    public MigrationProperty<T, X> AddProperty<X>(string name, MigrationPropertyOptions<X>? options = null)
    {
        return Table.AddProperty(name, options);
    }
    public MigrationProperty<T, int> AddPrimary<X>(string name)
    {
        return Table.AddPrimary(name);
    }

    public MigrationModel<T> AddTimestamp()
    {
        return Table.AddTimestamp();
    }

    public MigrationProperty<T, X> RenameProperty<X>(string currentName, string newName)
    {
       return Table.RenameProperty<X>(currentName, newName);
    }
    public MigrationProperty<T, object> RenameProperty(string currentName, string newName)
    {
       return Table.RenameProperty<object>(currentName, newName);
    }

    public MigrationProperty<T, X> RemoveProperty<X>(string name)
    {
       return Table.RemoveProperty<X>(name);
    }
     public MigrationProperty<T, object> RemoveProperty(string name)
    {
       return Table.RemoveProperty<object>(name);
    }
}
