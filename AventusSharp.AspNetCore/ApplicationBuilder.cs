using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AventusSharp.Chart;
using AventusSharp.Data;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Routes;
using AventusSharp.Routes.Response;
using AventusSharp.SSE;
using AventusSharp.Tools;
using AventusSharp.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using AventusSharp.AspNetCore.Routes;
using Environment = System.Environment;

namespace AventusSharp;

public static class AventusExtension
{
    public static bool IsExportCommand
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            return args.Contains("--export-info");
        }
    }

    public static bool IsDbDiagramCommand
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            return args.Contains("--db-diagram");
        }
    }

    private static void OnStop(this IApplicationBuilder app, Action action)
    {
        IHostApplicationLifetime lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        lifetime.ApplicationStopping.Register(() =>
        {
            action();
        });
    }
    public static IApplicationBuilder UseAventusData(this IApplicationBuilder app, Action<DataManagerConfig>? config = null)
    {
        return app.UseAventusData([Assembly.GetEntryAssembly()], config);
    }

    public static IApplicationBuilder UseAventusData(this IApplicationBuilder app, IEnumerable<Assembly?> assemblies, Action<DataManagerConfig>? config = null)
    {
        if (IsExportCommand) return app;

        ArgumentNullException.ThrowIfNull(assemblies);

        AventusLogger.Initialize(app.ApplicationServices.GetService<ILoggerFactory>());
        IDBStorage? db = app.ApplicationServices.GetService<IDBStorage>();

        if (config != null)
        {
            DataMainManager.Configure(config, db);
        }
        else if (db != null)
        {
            DataMainManager.Configure((config) => { }, db);
        }
        VoidWithError result = DataMainManager.Init(assemblies.ToList()).GetAwaiter().GetResult();
        if (!result.Success)
        {
            throw result.Errors[0].GetException();
        }
        return app;
    }

    public static IApplicationBuilder UseAventusHttp(this IApplicationBuilder app, Action<RouterConfig>? config = null)
    {
        return app.UseAventusHttp([Assembly.GetEntryAssembly()], config);
    }

    public static IApplicationBuilder UseAventusHttp(this IApplicationBuilder app, IEnumerable<Assembly?> assemblies, Action<RouterConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        AventusLogger.Initialize(app.ApplicationServices.GetService<ILoggerFactory>());

        if (config != null)
            Routes.RouterMiddleware.Configure(config);
        VoidWithError result = Routes.RouterMiddleware.Register(assemblies);
        if (!result.Success)
        {
            throw result.Errors[0].GetException();
        }
        app.Use((c, n) =>
            RouterAdapter.OnRequest(c, n)
        );

        return app;
    }

    public static IApplicationBuilder UseAventusWebsocket(this IApplicationBuilder app, Action<WebSocketConfig>? config = null)
    {
        return app.UseAventusWebsocket([Assembly.GetEntryAssembly()], config);
    }

    public static IApplicationBuilder UseAventusWebsocket(this IApplicationBuilder app, IEnumerable<Assembly?> assemblies, Action<WebSocketConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        AventusLogger.Initialize(app.ApplicationServices.GetService<ILoggerFactory>());

        if (config != null)
            WebSocketMiddleware.Configure(config);
        VoidWithError result = WebSocketMiddleware.Register(assemblies);
        if (!result.Success)
        {
            throw result.Errors[0].GetException();
        }

        app.OnStop(() =>
        {
            WebSocketMiddleware.Stop().Wait();
        });

        app.Use(WebSocketMiddleware.OnRequest);
        return app;
    }
    public static IApplicationBuilder UseAventusSSE(this IApplicationBuilder app, Action<SSEConfig>? config = null)
    {
        return app.UseAventusSSE([Assembly.GetEntryAssembly()], config);
    }

    public static IApplicationBuilder UseAventusSSE(this IApplicationBuilder app, IEnumerable<Assembly?> assemblies, Action<SSEConfig>? config = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        AventusLogger.Initialize(app.ApplicationServices.GetService<ILoggerFactory>());

        if (config != null)
            SSEMiddleware.Configure(config);
        VoidWithError result = SSEMiddleware.Register(assemblies);
        if (!result.Success)
        {
            throw result.Errors[0].GetException();
        }
        app.OnStop(() =>
        {
            SSEMiddleware.Stop().Wait();
        });
        app.Use(SSEMiddleware.OnRequest);
        return app;
    }

    public static IApplicationBuilder UseAventusExport(this IApplicationBuilder app)
    {
        if (IsExportCommand)
        {
            Routes.RouterMiddleware.PrintForExport();
            WebSocketMiddleware.PrintForExport();
            Environment.Exit(0);
        }
        return app;
    }

    public static IApplicationBuilder UseAventusDbDiagram(this IApplicationBuilder app, Action<DiagramConfig>? config = null)
    {
        if (IsDbDiagramCommand)
        {
            DiagramConfig baseConfig = new DiagramConfig()
            {
                GenerateMain = true,
                UseNamespaceForMain = true,
                MainName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Database",
                OutputDirectory = ""
            };
            if (config != null)
                config(baseConfig);

            List<IDBStorage> dbs = DBStorage.GetAll();
            List<DiagramObject> diagrams = new();
            foreach (IDBStorage db in dbs)
            {
                diagrams.AddRange(db.GetDiagrams(baseConfig.ToInternal()));
            }

            string output = baseConfig.OutputDirectory;
            if (!Path.IsPathFullyQualified(output))
            {
                output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, output);
            }

            foreach (DiagramObject diagramObject in diagrams)
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

                string txt = JsonConvert.SerializeObject(diagram, new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                });

                txt = txt.Replace("\r\n", "\n").Replace("\r", "\n");

                File.WriteAllText(writePath, txt);
            }

            Environment.Exit(0);
        }
        return app;
    }

}
