
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.SSE;

public class SSEMiddleware
{
    private static Action<SSEConfig> configAction = (config) => { };
    private static bool configLoaded = false;
    internal static readonly Dictionary<string, SSEEndPoint> endPointInstances = new();
    private static SSEEndPoint? mainEndPoint;

    internal static SSEConfig config = new SSEConfig();
    public static void Configure(Action<SSEConfig> configAction)
    {
        SSEMiddleware.configAction = configAction;

    }

    public static VoidWithError Register()
    {
        Assembly? entry = Assembly.GetEntryAssembly();
        if (entry != null)
        {
            return Register(entry);
        }
        VoidWithError result = new VoidWithError();
        result.Errors.Add(new SSEError(SSEErrorCode.CantDefineAssembly, "Can't determine the entry assembly"));
        return result;
    }

    public static VoidWithError Register(Assembly assembly)
    {
        List<Type> typesEndpoint = assembly.GetTypes().Where(p => p.GetInterfaces().Contains(typeof(ISSEEndPoint))).ToList();
        return Register(typesEndpoint);
    }

    public static VoidWithError Register(IEnumerable<Assembly?> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        List<Type> typesEndpoint = assemblies
            .Where(assembly => assembly is not null)
            .SelectMany(assembly => assembly!.GetTypes())
            .Where(type => type.GetInterfaces().Contains(typeof(ISSEEndPoint)))
            .Distinct()
            .ToList();
        return Register(typesEndpoint);
    }

    public static VoidWithError Register(IEnumerable<Type> typesEndpoint)
    {
        VoidWithError result;
        result = LoadConfig();
        if (!result.Success)
        {
            return result;
        }
        VoidWithSSEError resultTemp = RegisterEndPoints(typesEndpoint);
        result.Errors.AddRange(resultTemp.Errors);

        return result;
    }

    private static VoidWithError LoadConfig()
    {
        VoidWithSSEError result = new();
        if (!configLoaded)
        {
            try
            {
                configAction(config);
                configLoaded = true;
            }
            catch (Exception e)
            {
                result.Errors.Add(new SSEError(SSEErrorCode.ConfigError, e));
            }
        }
        return result.ToGeneric();
    }

    private static VoidWithSSEError RegisterEndPoints(IEnumerable<Type> typesEndpoint)
    {
        VoidWithSSEError result = new();
        foreach (Type t in typesEndpoint)
        {
            if (t.IsAbstract)
            {
                continue;
            }

            SSEEndPoint? endPoint = (SSEEndPoint?)Activator.CreateInstance(t);
            if (endPoint != null)
            {
                string path = endPoint.Path;
                if (endPointInstances.ContainsKey(path))
                {
                    continue;
                }
                endPointInstances[path] = endPoint;

                if (endPoint.Main())
                {
                    if (mainEndPoint == null)
                    {
                        mainEndPoint = endPoint;
                    }
                    else
                    {
                        string previous = mainEndPoint.GetType().FullName ?? "";
                        string current = endPoint.GetType().FullName ?? "";
                        result.Errors.Add(new SSEError(SSEErrorCode.MultipleMainEndpoint, "You can't define multiple main endpoint : " + previous + " and " + current));
                    }
                }
            }
        }

        if (endPointInstances.Count() == 1 && mainEndPoint == null)
        {
            mainEndPoint = endPointInstances.ElementAt(0).Value;
        }

        if (mainEndPoint == null)
        {
            GetMain();
        }

        return result;
    }

    internal static SSEEndPoint GetMain()
    {
        if (mainEndPoint == null)
        {
            mainEndPoint = new DefaultSSEEndPoint();
            endPointInstances.Add(mainEndPoint.Path, mainEndPoint);
        }
        return mainEndPoint;
    }
    public async static Task OnRequest(HttpContext context, Func<Task> next)
    {
        string newPath = context.Request.Path.ToString();
        if (endPointInstances.ContainsKey(newPath))
        {
            await endPointInstances[newPath].StartNewInstance(context);
        }
        else
        {
            await next();
        }
    }

    public static async Task Stop()
    {
        foreach (KeyValuePair<string, SSEEndPoint> endpoint in endPointInstances)
        {
            await endpoint.Value.Stop();
        }
    }

}
