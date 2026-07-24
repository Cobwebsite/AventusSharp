using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AventusSharp.Tools;

namespace AventusSharp.Data.Migrations;


public static class MigrationManager
{
    private static List<Type> _migrationsClasses = new List<Type>();
    public static async Task<VoidWithError> Run(List<Assembly> assemblies)
    {
        VoidWithError result = new VoidWithError();
        result.Run(() => LoadMigrations(assemblies));
        await result.RunAsync(ApplyMigrations);



        return result;
    }

    private static VoidWithError LoadMigrations(List<Assembly> assemblies)
    {
        VoidWithError result = new VoidWithError();
        _migrationsClasses.Clear();
        foreach (Assembly assembly in assemblies)
        {
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                if (type.GetInterfaces().Contains(typeof(IMigration)) && !type.IsAbstract && !_migrationsClasses.Contains(type))
                {
                    _migrationsClasses.Add(type);
                }
            }
        }
        return result;
    }

    private static async Task<VoidWithError> ApplyMigrations()
    {
        VoidWithError result = new VoidWithError();

        if (_migrationsClasses.Count > 0)
        {
            // we prepare all providers that comes from our DM
            List<IMigrationProvider> providers = MigrationFactory.GetAll();
            if (providers.Count > 1 && !DataMainManager.Config.Migration.MultipleProviders)
            {
                result.Errors.Add(new DataError(DataErrorCode.MultipleProvidersNotSet, "You must set MultipleProviders to true inside the Migration configuration"));
                return result;
            }
            foreach (IMigrationProvider provider in providers)
            {
                await result.RunAsync(provider.Init);
            }

            SortedDictionary<string, Migration> migrations = new SortedDictionary<string, Migration>();
            foreach (var migrationClass in _migrationsClasses)
            {
                object? _migration = Activator.CreateInstance(migrationClass);
                if (_migration is Migration migration)
                {
                    string name = migration.GetName();
                    migrations.Add(name, migration);
                }
            }

            foreach (KeyValuePair<string, Migration> migrationPair in migrations)
            {
                await result.RunAsync(() => migrationPair.Value._Up(providers));
            }
        }
        return result;
    }
}