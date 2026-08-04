using AventusSharp.Data;
using AventusSharp.Hosting;
using AventusSharp.Maui;
using AventusSharp.Routes;
using AventusSharp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// Registers the portable router dispatcher and the MAUI WebView bridge.
    /// Call this before <see cref="MauiAppBuilder.Build"/>.
    /// </summary>
    public static MauiAppBuilder AddAventus(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IAventusRequestDispatcher,
            AventusRequestDispatcher>();
        builder.Services.TryAddSingleton<AventusMauiBridge>(services =>
            new AventusMauiBridge(
                services.GetRequiredService<IAventusRequestDispatcher>(),
                services.GetRequiredService<IServiceScopeFactory>()));

        return builder;
    }

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

        if (config is not null)
        {
            DataMainManager.Configure(config);
        }

        VoidWithError result = DataMainManager.Init(assemblies.ToList())
            .GetAwaiter()
            .GetResult();
        ThrowOnError(result);
        return app;
    }

    /// <summary>
    /// Registers the existing AventusSharp routes for execution through
    /// <see cref="AventusMauiBridge"/>.
    /// </summary>
    public static MauiApp UseAventusHttp(
        this MauiApp app,
        Action<RouterConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        InitializeLogger(app);

        if (config is not null)
        {
            RouterMiddleware.Configure(config);
        }

        VoidWithError result = RouterMiddleware.Register();
        ThrowOnError(result);

        // Fail at startup with a clear DI error if AddAventus was omitted.
        _ = app.Services.GetRequiredService<AventusMauiBridge>();
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
