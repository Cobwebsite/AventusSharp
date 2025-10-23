using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;

namespace AventusSharp.Data.Migrations;

public class MigrationFactory
{
    public static readonly MigrationFactory Instance = new MigrationFactory();

    private static Dictionary<Type, IMigrationProvider> _types = new Dictionary<Type, IMigrationProvider>();
    private static List<IMigrationProvider> _instances = new List<IMigrationProvider>();
    public static IMigrationProvider Make<T>() where T : IMigrationProvider, new()
    {
        Type t = typeof(T);
        if (!_types.ContainsKey(t))
        {
            object? instance = Activator.CreateInstance(t);
            if (instance is IMigrationProvider provider)
                _types.Add(t, provider);
        }
        return _types[t];
    }

    public static IMigrationProvider Register<T>(T instance) where T : IMigrationProvider
    {
        if (!_instances.Contains(instance))
        {
            _instances.Add(instance);
        }
        return instance;
    }

    internal static List<IMigrationProvider> GetAll()
    {
        List<IMigrationProvider> providers = _instances.ToList();
        foreach (KeyValuePair<Type, IMigrationProvider> pair in _types)
        {
            providers.Add(pair.Value);
        }

        return providers;
    }

    private MigrationFactory() { }

}
public interface IMigrationProvider
{
    VoidWithError Init();
    ResultWithError<bool> Can(string name);
    VoidWithError Save(string name);
    void BeforeUp(VoidWithError voidWithError);
    void AfterUp(VoidWithError voidWithError);
    VoidWithError ApplyMigration<X>(IMigrationModel model) where X : notnull, IStorable;

}
public abstract class MigrationProvider : IMigrationProvider
{
    public abstract VoidWithError Init();
    public abstract ResultWithError<bool> Can(string name);
    public abstract VoidWithError Save(string name);
    public abstract void BeforeUp(VoidWithError voidWithError);
    public abstract void AfterUp(VoidWithError voidWithError);
    public abstract VoidWithError ApplyMigration<X>(IMigrationModel model) where X : notnull, IStorable;

    protected void InitMigrationTableDM()
    {
        Type type = typeof(MigrationTable);

        if (DataMainManager.DefaultDMType == null) return;

        Type simpleType = DataMainManager.DefaultDMType.MakeGenericType([type]);
        MethodInfo? GetInstance = simpleType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (GetInstance == null)
        {
            return;
        }
        IGenericDM? simpleManager = (IGenericDM?)GetInstance.Invoke(null, null);
        if (simpleManager == null)
        {
            return;
        }

        List<DataMemberInfo> memberInfos = new();
        FieldInfo[] fields = type.GetFields();
        foreach (FieldInfo field in fields)
        {
            memberInfos.Add(new(field));
        }
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            memberInfos.Add(new(property));
        }

        PyramidInfo pyramid = new PyramidInfo(type, memberInfos);
        simpleManager.SetConfiguration(pyramid, DataMainManager.Config);

    }
}