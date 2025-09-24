using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket.Attributes;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AventusSharp.WebSocket
{
    public class WebSocketMiddleware
    {
        private static Dictionary<Type, IWsRouter> routeInstances = new Dictionary<Type, IWsRouter>();
        internal static readonly Dictionary<string, WsEndPoint> endPointInstances = new();
        private static WsEndPoint? mainEndPoint;


        private static Action<WebSocketConfig> configAction = (config) => { };
        private static bool configLoaded = false;
        internal static WebSocketConfig config = new WebSocketConfig();
        internal static Dictionary<Type, object> injected = new Dictionary<Type, object>();

        public static Dictionary<WsEndPoint, List<WebSocketRouteInfo>> GetAllRoutes()
        {
            return endPointInstances.ToDictionary(p => p.Value, p => p.Value.routesInfo.Values.ToList());
        }
        public static void Configure(Action<WebSocketConfig> configAction)
        {
            WebSocketMiddleware.configAction = configAction;

        }

        public static void Inject(object o)
        {
            injected[o.GetType()] = o;
        }
        public static void Inject(Type type, object o)
        {
            injected[type] = o;
        }
        public static void Inject<T>(T o) where T : notnull
        {
            injected[o.GetType()] = o;
        }
        public static void Inject<T, U>() where T : notnull where U : T
        {
            object? o = Activator.CreateInstance(typeof(U));
            if (o != null)
                injected[typeof(T)] = o;
            else
                Console.WriteLine("Can't create " + typeof(U));
        }
        public static WebSocketConnection? GetConnection<T>(string sessionId) where T : WsEndPoint
        {
            WsEndPoint? endPoint = endPointInstances.Values.FirstOrDefault(p => p.GetType() == typeof(T));
            if (endPoint == null) return null;

            WebSocketConnection? connection = endPoint.connections.FirstOrDefault(p => p.SessionId == sessionId);
            return connection;
        }

        public static async Task Stop()
        {
            foreach (KeyValuePair<string, WsEndPoint> endpoint in endPointInstances)
            {
                await endpoint.Value.Stop();
            }
        }
        public static VoidWithError Register()
        {
            Assembly? entry = Assembly.GetEntryAssembly();
            if (entry != null)
            {
                return Register(entry);
            }
            VoidWithError result = new VoidWithError();
            result.Errors.Add(new WsError(WsErrorCode.CantDefineAssembly, "Can't determine the entry assembly"));
            return result;
        }

        public static VoidWithError Register(Assembly assembly)
        {
            List<Type> typesEndpoint = assembly.GetTypes().Where(p => p.GetInterfaces().Contains(typeof(IWsEndPoint))).ToList();
            List<Type> typesRoute = assembly.GetTypes().Where(p => p.GetInterfaces().Contains(typeof(IWsRouter))).ToList();
            return Register(typesEndpoint, typesRoute);
        }

        public static VoidWithError Register(IEnumerable<Type> typesEndpoint, IEnumerable<Type> typesRoute)
        {
            VoidWithError result;
            result = LoadConfig();
            if (!result.Success)
            {
                return result;
            }
            VoidWithWsError resultTemp = RegisterEndPoints(typesEndpoint);
            result.Errors.AddRange(resultTemp.Errors);
            RegisterRoutes(typesRoute);

            return result;
        }

        internal static WsEndPoint GetMain()
        {
            if (mainEndPoint == null)
            {
                mainEndPoint = new DefaultWsEndPoint();
                endPointInstances.Add(mainEndPoint.Path, mainEndPoint);
            }
            return mainEndPoint;
        }
        private static VoidWithWsError RegisterEndPoints(IEnumerable<Type> typesEndpoint)
        {
            VoidWithWsError result = new();
            foreach (Type t in typesEndpoint)
            {
                if (t.IsAbstract)
                {
                    continue;
                }

                WsEndPoint? endPoint = (WsEndPoint?)Activator.CreateInstance(t);
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
                            result.Errors.Add(new WsError(WsErrorCode.MultipleMainEndpoint, "You can't define multiple main endpoint : " + previous + " and " + current));
                        }
                    }
                }
            }

            if (endPointInstances.Count() == 1 && mainEndPoint == null)
            {
                mainEndPoint = endPointInstances.ElementAt(0).Value;
            }

            return result;
        }
        private static void RegisterRoutes(IEnumerable<Type> typesRoute)
        {
            Func<string, Dictionary<string, WebSocketRouterParameterInfo>, object, bool, string> transformPattern = config.transformPattern ?? PrepareUrl;
            Func<string, Regex> transformPatternIntoRegex = config.transformPatternIntoRegex ?? PrepareRegex;

            foreach (Type t in typesRoute)
            {
                if (routeInstances.ContainsKey(t))
                {
                    continue;
                }

                if (t.IsAbstract)
                {
                    continue;
                }

                IWsRouter? routeTemp = (IWsRouter?)Activator.CreateInstance(t);
                if (routeTemp != null)
                {
                    routeInstances[t] = routeTemp;
                }

                // load end point class
                List<WsEndPoint> endpointsClass = new List<WsEndPoint>();
                List<Attribute> routeAttributes = t.GetCustomAttributes().ToList();
                Dictionary<Type, WsEndPoint> availables = endPointInstances.Values.ToDictionary(t => t.GetType(), t => t);
                string prefix = "";

                foreach (Attribute routeAttribute in routeAttributes)
                {
                    if (routeAttribute is Attributes.EndPoint endPointAttr)
                    {
                        if (availables.ContainsKey(endPointAttr.endpoint))
                        {
                            WsEndPoint endpoint = availables[endPointAttr.endpoint];
                            if (!endpointsClass.Contains(endpoint))
                            {
                                endpointsClass.Add(endpoint);
                            }
                        }
                    }
                    else if (routeAttribute is Prefix prefixAttr)
                    {
                        prefix = prefixAttr.txt;
                    }
                }

                List<MethodInfo> methods = t.GetMethods().ToList();

                foreach (MethodInfo method in methods)
                {
                    string fullName = method.DeclaringType?.Assembly.FullName ?? "";
                    if (!method.IsPublic || fullName.StartsWith("System."))
                    {
                        continue;
                    }

                    List<WebSocketRouterParameterInfo> fctParams = new List<WebSocketRouterParameterInfo>();
                    ParameterInfo[] parameters = method.GetParameters();
                    foreach (ParameterInfo parameter in parameters)
                    {
                        fctParams.Add(new WebSocketRouterParameterInfo(parameter.Name ?? "", parameter.ParameterType)
                        {
                            positionCSharp = parameter.Position,
                            optional = parameter.IsOptional
                        });
                    }

                    List<Attribute> methodsAttribute = method.GetCustomAttributes().ToList();
                    WebSocketAttributeAnalyze infoMethod = PrepareAttributes(methodsAttribute, prefix);

                    if (!infoMethod.canUse) continue;

                    if (infoMethod.endPoints.Count == 0)
                    {
                        if (endpointsClass.Count == 0)
                        {
                            infoMethod.endPoints.Add(GetMain());
                        }
                        else
                        {
                            infoMethod.endPoints.Add(endpointsClass[0]);
                        }
                    }

                    if (infoMethod.pathes.Count == 0)
                    {
                        infoMethod.pathes.Add(prefix + Tools.GetDefaultMethodUrl(method));
                    }

                    foreach (string route in infoMethod.pathes)
                    {
                        Dictionary<string, WebSocketRouterParameterInfo> @params = fctParams.ToDictionary(p => p.name, p => p);
                        string urlPattern = route;
                        string url = transformPattern(urlPattern, @params, routeInstances[t], false);
                        Regex regex = transformPatternIntoRegex(url);

                        foreach (IWsEndPoint endpointType in infoMethod.endPoints)
                        {
                            WebSocketRouteInfo info = new WebSocketRouteInfo(regex, method, routeInstances[t], parameters.Length, infoMethod.eventType, infoMethod.CustomFct);
                            info.parameters = @params;

                            if (endpointType is WsEndPoint _class)
                            {
                                if (!_class.routesInfo.ContainsKey(info.UniqueKey))
                                {
                                    info.router.AddEndPoint(_class);
                                    info.endpoint = _class;
                                    _class.routesInfo.Add(info.UniqueKey, info);
                                    if (config.PrintRoute)
                                        Console.WriteLine("Add websocket : " + info.ToString());
                                }
                                else
                                {
                                    if (config.PrintRoute)
                                        Console.WriteLine("Add websocket : " + info.ToString());
                                    WebSocketRouteInfo otherInfo = _class.routesInfo[info.UniqueKey];
                                    throw new Exception(info.ToString() + " is already added from " + otherInfo.action.Name + " (" + otherInfo.action.DeclaringType?.Assembly.FullName + ")");
                                }
                            }

                        }
                    }

                }
            }
        }

        internal static WebSocketAttributeAnalyze PrepareAttributes(IEnumerable<object> attrs, string prefix)
        {
            WebSocketAttributeAnalyze info = new WebSocketAttributeAnalyze();
            Dictionary<Type, WsEndPoint> availables = endPointInstances.Values.ToDictionary(t => t.GetType(), t => t);
            foreach (object attr in attrs)
            {
                PrepareAttributesPart(attr, info, availables, prefix);
            }
            return info;
        }
        internal static WebSocketAttributeAnalyze PrepareAttributes(IEnumerable<Attribute> attrs, string prefix)
        {
            WebSocketAttributeAnalyze info = new WebSocketAttributeAnalyze();
            Dictionary<Type, WsEndPoint> availables = endPointInstances.Values.ToDictionary(t => t.GetType(), t => t);
            foreach (object attr in attrs)
            {
                PrepareAttributesPart(attr, info, availables, prefix);
            }
            return info;
        }
        internal static void PrepareAttributesPart(object attr, WebSocketAttributeAnalyze info, Dictionary<Type, WsEndPoint> availables, string prefix)
        {
            if (attr is Path pathAttr)
            {
                string pattern = prefix + pathAttr.pattern;
                if (!info.pathes.Contains(pattern))
                {
                    info.pathes.Add(pattern);
                }
            }
            else if (attr is Attributes.EndPoint endPointAttr)
            {
                if (availables.ContainsKey(endPointAttr.endpoint))
                {
                    WsEndPoint endpoint = availables[endPointAttr.endpoint];
                    if (!info.endPoints.Contains(endpoint))
                    {
                        info.endPoints.Add(endpoint);
                    }
                }
            }
            else if (attr is ResponseType responseType)
            {
                if (info.eventType < responseType.Type)
                {
                    info.eventType = responseType.Type;
                }
                info.CustomFct = responseType.CustomFct;
            }
            else if (attr is NoRoute)
            {
                info.canUse = false;
            }
        }

        private static VoidWithError LoadConfig()
        {
            VoidWithWsError result = new();
            if (!configLoaded)
            {
                try
                {
                    configAction(config);
                }
                catch (Exception e)
                {
                    result.Errors.Add(new WsError(WsErrorCode.ConfigError, e));
                }
            }
            return result.ToGeneric();
        }

        public static string PrepareUrl(string urlPattern, Dictionary<string, WebSocketRouterParameterInfo> @params, object o, bool isEvent)
        {
            urlPattern = ReplaceParams(urlPattern, @params);
            urlPattern = ReplaceFunction(urlPattern, o);
            return urlPattern;
        }
        public static string ReplaceFunction(string urlPattern, object o)
        {
            MatchCollection matchingFct = new Regex("\\[[a-zA-Z0-9_]*?\\]").Matches(urlPattern);

            if (matchingFct.Count > 0)
            {
                foreach (Match match in matchingFct)
                {
                    string value = match.Value.Replace("[", "").Replace("]", "");
                    MethodInfo? method = o.GetType().GetMethod(value, BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (method == null)
                    {
                        Console.WriteLine("Can't find method " + value + " on " + o.GetType().FullName);
                        continue;
                    }
                    //object? res = method.Invoke(routeInstances[t], Array.Empty<object>());
                    object? res = method.Invoke(o, Array.Empty<object>());
                    if (res != null)
                    {
                        urlPattern = urlPattern.Replace(match.Value, res.ToString());
                    }
                }
            }
            return urlPattern;
        }
        public static string ReplaceParams(string urlPattern, Dictionary<string, WebSocketRouterParameterInfo> @params)
        {
            MatchCollection matching = new Regex("{.*?}").Matches(urlPattern);
            int i = 0;
            foreach (Match match in matching)
            {
                string value = match.Value.Replace("{", "").Replace("}", "");
                if (@params.ContainsKey(value))
                {
                    if (@params[value].type == typeof(int))
                    {
                        urlPattern = urlPattern.Replace(match.Value, "([0-9]+)");
                    }
                    else if (@params[value].type == typeof(string))
                    {
                        urlPattern = urlPattern.Replace(match.Value, "([^/]+)");
                    }
                    @params[value].positionUrl = i;
                }
                else
                {
                    urlPattern = urlPattern.Replace(match.Value, "([^/]+)");
                }
                i++;
            }
            return urlPattern;
        }

        public static Regex PrepareRegex(string urlPattern)
        {
            if (!urlPattern.StartsWith("^"))
            {
                urlPattern = "^" + urlPattern;
            }
            if (!urlPattern.EndsWith("$"))
            {
                urlPattern += "$";
            }

            string replaceSlash = @"([a-zA-Z0-9_-]|^)\/";
            urlPattern = Regex.Replace(urlPattern, replaceSlash, "$1\\/");
            urlPattern = urlPattern.ToLower();
            return new Regex(urlPattern);
        }



        /// <summary>
        /// Middleware function
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public async static Task OnRequest(HttpContext context, Func<Task> next)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                string newPath = context.Request.Path.ToString();
                if (endPointInstances.ContainsKey(newPath))
                {
                    System.Net.WebSockets.WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await endPointInstances[newPath].StartNewInstance(context, webSocket);
                }
                //else
                //{
                //    context.Response.StatusCode = 404;
                //    if (enableError)
                //    {
                //        Console.WriteLine("no router found for " + newPath);
                //        string listRouter = string.Join(", ", routers.Keys.ToList());
                //        Console.WriteLine("List " + listRouter);
                //    }
                //}
            }

            await next();
        }
    }
}
