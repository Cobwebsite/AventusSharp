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
using Newtonsoft.Json;
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
        if (IsExportCommand) return app;

        AventusLogger.Initialize(app);

        if (config != null)
            DataMainManager.Configure(config);
        VoidWithError result = DataMainManager.Init().GetAwaiter().GetResult();
        if (!result.Success)
        {
            throw result.Errors[0].GetException();
        }
        return app;
    }

    public static IApplicationBuilder UseAventusHttp(this IApplicationBuilder app, Action<RouterConfig>? config = null)
    {
        AventusLogger.Initialize(app);

        if (config != null)
            Routes.RouterMiddleware.Configure(config);
        VoidWithError result = Routes.RouterMiddleware.Register();
        if (!result.Success)
        {
            throw result.Errors[0].GetException();
        }
        app.Use(Routes.RouterMiddleware.OnRequest);

        return app;
    }

    public static IApplicationBuilder UseAventusWebsocket(this IApplicationBuilder app, Action<WebSocketConfig>? config = null)
    {
        AventusLogger.Initialize(app);

        if (config != null)
            WebSocketMiddleware.Configure(config);
        VoidWithError result = WebSocketMiddleware.Register();
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
        AventusLogger.Initialize(app);

        if (config != null)
            SSEMiddleware.Configure(config);
        VoidWithError result = SSEMiddleware.Register();
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
                    NullValueHandling = NullValueHandling.Ignore
                });
                if (Environment.NewLine != "\n")
                    txt = txt.Replace(Environment.NewLine, "\n");
                File.WriteAllText(writePath, txt);
            }

            Environment.Exit(0);
        }
        return app;
    }

}