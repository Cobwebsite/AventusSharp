using AventusSharp.Data;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Routes;
using AventusSharp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using System.Reflection;
using AventusSharp.Localization;
using AventusSharp.Scheduler;
using AventusSharp.Chart;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace AventusSharp;

/// <summary>
/// Configures AventusSharp for an in-process .NET MAUI application.
/// </summary>
public static class AventusMauiExtension
{
    public static bool IsExportCommand => Environment.GetCommandLineArgs().Contains("--export-info");

    public static bool IsDbDiagramCommand => Environment.GetCommandLineArgs().Contains("--db-diagram");

    /// <summary>Configures translations for the desktop application, including the in-process bridge.</summary>
    public static MauiApp UseAventusTranslations(this MauiApp app, Action<AventusTranslationOptions>? configure = null)
    {
        AventusTranslations.Configure(options => configure?.Invoke(options));
        return app;
    }

    /// <summary>
    /// Initializes the AventusSharp data managers and configured providers.
    /// Call this after <see cref="MauiAppBuilder.Build"/>.
    /// </summary>
    public static MauiApp UseAventusData(this MauiApp app, Action<DataManagerConfig>? config = null)
    {
        return app.UseAventusData([Assembly.GetEntryAssembly()], config);
    }

    /// <summary>
    /// Initializes the AventusSharp data managers by scanning the supplied
    /// assemblies for models and managers.
    /// </summary>
    public static MauiApp UseAventusData(this MauiApp app, IEnumerable<Assembly?> assemblies, Action<DataManagerConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(assemblies);
        if (IsExportCommand) return app;
        InitializeLogger(app);

        IDBStorage? db = app.Services.GetService<IDBStorage>();
        if (config != null)
        {
            DataMainManager.Configure(config, db);
        }
        else if (db != null)
        {
            DataMainManager.Configure((config) => { }, db);
        }

        VoidWithError result = Task.Run(() => DataMainManager.Init(assemblies.ToList())).GetAwaiter().GetResult();
        ThrowOnError(result);
        return app;
    }

    /// <summary>
    /// Registers the existing AventusSharp routes for execution through
    /// </summary>
    public static MauiApp UseAventusHttp(this MauiApp app, Action<RouterConfig>? config = null)
    {
        return app.UseAventusHttp([Assembly.GetEntryAssembly()], config);
    }

    /// <summary>
    /// Registers AventusSharp routes found in the supplied assemblies.
    /// </summary>
    public static MauiApp UseAventusHttp(this MauiApp app, IEnumerable<Assembly?> assemblies, Action<RouterConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(assemblies);
        InitializeLogger(app);

        if (config is not null)
        {
            RouterMiddleware.Configure(config);
        }

        VoidWithError result = RouterMiddleware.Register(assemblies);
        ThrowOnError(result);

        return app;
    }

    /// <summary>Discovers and starts schedulable tasks from the entry assembly.</summary>
    public static MauiApp UseAventusScheduler(this MauiApp app, Action<SchedulerManagerConfig>? config = null)
    {
        return app.UseAventusScheduler([Assembly.GetEntryAssembly()], config);
    }

    /// <summary>Discovers and starts schedulable tasks from the supplied assemblies.</summary>
    public static MauiApp UseAventusScheduler(this MauiApp app, IEnumerable<Assembly?> assemblies, Action<SchedulerManagerConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(assemblies);
        InitializeLogger(app);

        SchedulerManager.Configure(options =>
        {
            config?.Invoke(options);
            options.CreateSchedulable ??= type =>
                app.Services.GetService(type) as ISchedulable
                ?? ActivatorUtilities.CreateInstance(app.Services, type) as ISchedulable;
        });
        VoidWithError result = Task.Run(() => SchedulerManager.Init(assemblies)).GetAwaiter().GetResult();
        ThrowOnError(result);
        app.Services.GetService<IHostApplicationLifetime>()?.ApplicationStopping.Register(SchedulerManager.Stop);
        return app;
    }

    /// <summary>Prints registered HTTP routes when launched with --export-info.</summary>
    public static MauiApp UseAventusExport(this MauiApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (IsExportCommand)
        {
            RouterMiddleware.PrintForExport();
            Environment.Exit(0);
        }
        return app;
    }

    /// <summary>Writes database diagrams when launched with --db-diagram.</summary>
    public static MauiApp UseAventusDbDiagram(this MauiApp app, Action<DiagramConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!IsDbDiagramCommand) return app;

        var options = new DiagramConfig
        {
            GenerateMain = true,
            UseNamespaceForMain = true,
            MainName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Database",
            OutputDirectory = ""
        };
        config?.Invoke(options);

        string output = options.OutputDirectory;
        if (!Path.IsPathFullyQualified(output))
        {
            output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, output);
        }

        foreach (DiagramObject diagramObject in DBStorage.GetAll().SelectMany(db => db.GetDiagrams(options.ToInternal())))
        {
            DiagramObject diagram = diagramObject;
            string writePath = Path.Join(output, diagram.Name + ".db.avt");
            if (File.Exists(writePath))
            {
                DiagramObject? oldDiagram = JsonConvert.DeserializeObject<DiagramObject>(File.ReadAllText(writePath));
                if (oldDiagram != null)
                {
                    oldDiagram.Merge(diagram);
                    diagram = oldDiagram;
                }
            }

            string json = JsonConvert.SerializeObject(diagram, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            });
            File.WriteAllText(writePath, json.Replace("\r\n", "\n").Replace("\r", "\n"));
        }

        Environment.Exit(0);
        return app;
    }

    private static void InitializeLogger(MauiApp app)
    {
        AventusLogger.Initialize(app.Services.GetService<ILoggerFactory>(), null);
    }

    private static void ThrowOnError(VoidWithError result)
    {
        if (!result.Success)
        {
            throw result.Errors[0].GetException();
        }
    }
}
