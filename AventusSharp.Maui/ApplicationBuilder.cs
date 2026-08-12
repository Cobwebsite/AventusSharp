using AventusSharp.Data;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Routes;
using AventusSharp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using System.Reflection;

namespace AventusSharp;

/// <summary>
/// Configures AventusSharp for an in-process .NET MAUI application.
/// </summary>
public static class AventusMauiExtension
{
    /// <summary>
    /// Initializes the AventusSharp data managers and configured providers.
    /// Call this after <see cref="MauiAppBuilder.Build"/>.
    /// </summary>
    public static MauiApp UseAventusData(
        this MauiApp app,
        Action<DataManagerConfig>? config = null)
    {
        return app.UseAventusData(
            [Assembly.GetEntryAssembly()],
            config);
    }

    /// <summary>
    /// Initializes the AventusSharp data managers by scanning the supplied
    /// assemblies for models and managers.
    /// </summary>
    public static MauiApp UseAventusData(
        this MauiApp app,
        IEnumerable<Assembly?> assemblies,
        Action<DataManagerConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(assemblies);
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

        VoidWithError result = DataMainManager.Init(assemblies.ToList())
            .GetAwaiter()
            .GetResult();
        ThrowOnError(result);
        return app;
    }

    /// <summary>
    /// Registers the existing AventusSharp routes for execution through
    /// </summary>
    public static MauiApp UseAventusHttp(
        this MauiApp app,
        Action<RouterConfig>? config = null)
    {
        return app.UseAventusHttp(
            [Assembly.GetEntryAssembly()],
            config);
    }

    /// <summary>
    /// Registers AventusSharp routes found in the supplied assemblies.
    /// </summary>
    public static MauiApp UseAventusHttp(
        this MauiApp app,
        IEnumerable<Assembly?> assemblies,
        Action<RouterConfig>? config = null)
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

    private static void InitializeLogger(MauiApp app) =>
        AventusLogger.Initialize(app.Services.GetService<ILoggerFactory>());

    private static void ThrowOnError(VoidWithError result)
    {
        if (!result.Success)
        {
            throw result.Errors[0].GetException();
        }
    }
}
