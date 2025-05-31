using System;
using System.Collections.Generic;
using System.Linq;
using AventusSharp.Tools;

namespace AventusSharp.Data.Migrations;

public class MigrationFactory
{
    public static readonly MigrationFactory Instance = new MigrationFactory();

    private static List<Type> _types = new List<Type>();
    private static List<IMigrationProvider> _instances = new List<IMigrationProvider>();
    public static MigrationFactory Make<T>() where T : IMigrationProvider, new()
    {
        Type t = typeof(T);
        if (!_types.Contains(t))
        {
            _types.Add(t);
        }
        return Instance;
    }

    public static MigrationFactory Register<T>(T instance) where T : IMigrationProvider
    {
        if (!_instances.Contains(instance))
        {
            _instances.Add(instance);
        }
        return Instance;
    }

    internal static List<IMigrationProvider> GetAll()
    {
        List<IMigrationProvider> providers = _instances.ToList();
        foreach (Type type in _types)
        {
            object? instance = Activator.CreateInstance(type);
            if (instance is IMigrationProvider provider)
                providers.Add(provider);
        }

        return providers;
    }

    private MigrationFactory() { }

}
public interface IMigrationProvider
{
    VoidWithError Init();
}
public abstract class MigrationProvider : IMigrationProvider
{
    public abstract VoidWithError Init();
}