using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AventusSharp.Mcp;
using AventusSharp.Mcp.Attributes;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Request;
using AventusSharp.Routes.Response;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.Routes
{
    public static class RouterMiddleware
    {
        private static Dictionary<Type, IRouter> routerInstances = new Dictionary<Type, IRouter>();
        private static Dictionary<string, RouteInfo> routesInfo = new Dictionary<string, RouteInfo>();
        private static Action<RouterConfig> configAction = (config) => { };
        private static bool configLoaded = false;
        internal static RouterConfig config = new RouterConfig();
        private static Dictionary<Type, object> injected = new Dictionary<Type, object>();

        public static void Configure(Action<RouterConfig> configAction)
        {
            RouterMiddleware.configAction = configAction;
        }

        public static List<RouteInfo> GetAllRoutes()
        {
            return routesInfo.Values.ToList();
        }

        public static VoidWithError Register()
        {
            Assembly? entry = Assembly.GetEntryAssembly();
            if (entry != null)
            {
                return Register(entry);
            }
            return new VoidWithError();
        }

        public static VoidWithError Register(Assembly assembly)
        {
            List<Type> types = assembly.GetTypes().Where(p => p.GetInterfaces().Contains(typeof(IRouter))).ToList();
            return Register(types);
        }

        public static VoidWithError Register(IEnumerable<Type> types)
        {
            VoidWithRouteError result = new VoidWithRouteError();
            LoadConfig();
            Func<string, Dictionary<string, RouterParameterInfo>, Type, MethodInfo, Regex> transformPattern = config.transformPattern ?? PrepareUrl;
            McpMiddleware.ClearHttpMethods();
            foreach (Type t in types)
            {
                if (routerInstances.ContainsKey(t))
                {
                    continue;
                }

                if (!t.IsAbstract)
                {
                    IRouter? routerTemp = (IRouter?)Activator.CreateInstance(t);
                    if (routerTemp != null)
                    {
                        routerInstances[t] = routerTemp;
                    }

                    List<Attribute> routeAttributes = t.GetCustomAttributes().ToList();
                    string prefix = "";
                    foreach (Attribute routeAttribute in routeAttributes)
                    {
                        if (routeAttribute is Prefix prefixAttr)
                        {
                            prefix = prefixAttr.txt;
                        }
                    }
                    List<MethodInfo> methods = t.GetMethods()
                                                //.Where(p => p.GetCustomAttributes().Where(p1 => p1 is Attributes.Path).Count() > 0)
                                                .ToList();

                    foreach (MethodInfo method in methods)
                    {
                        string fullName = method.DeclaringType?.Assembly.FullName ?? "";
                        if (!method.IsPublic || fullName.StartsWith("System."))
                        {
                            continue;
                        }

                        List<string> routes = new List<string>();
                        List<Attribute> methodsAttribute = method.GetCustomAttributes().ToList();
                        List<MethodType> methodsToUse = new List<MethodType>();
                        bool canUse = true;
                        bool addMcp = false;
                        foreach (Attribute methodAttribute in methodsAttribute)
                        {
                            if (methodAttribute is Attributes.Path pathAttr)
                            {
                                string pattern = prefix + pathAttr.pattern;
                                if (!routes.Contains(pattern))
                                {
                                    routes.Add(pattern);
                                }
                            }
                            else if (methodAttribute is Get) { methodsToUse.Add(MethodType.Get); }
                            else if (methodAttribute is Post) { methodsToUse.Add(MethodType.Post); }
                            else if (methodAttribute is Put) { methodsToUse.Add(MethodType.Put); }
                            else if (methodAttribute is Options) { methodsToUse.Add(MethodType.Options); }
                            else if (methodAttribute is Delete) { methodsToUse.Add(MethodType.Delete); }
                            else if (methodAttribute is NoRoute)
                            {
                                canUse = false;
                            }
                            else if (methodAttribute is McpTool)
                            {
                                addMcp = true;
                            }
                        }
                        if (!canUse) continue;

                        List<RouterParameterInfo> fctParams = new List<RouterParameterInfo>();
                        ParameterInfo[] parameters = method.GetParameters();

                        if (routes.Count == 0)
                        {
                            string defaultName = prefix + Tools.GetDefaultMethodUrl(method);
                            routes.Add(defaultName);
                        }


                        bool hasBody = false;
                        foreach (ParameterInfo parameterInfo in parameters)
                        {
                            Type type = parameterInfo.ParameterType;
                            bool mandatory = true;
                            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                type = type.GetGenericArguments()[0];
                                mandatory = false;
                            }
                            else if (parameterInfo.DefaultValue != null)
                            {
                                mandatory = false;
                            }
                            RouterParameterInfo parameter = new RouterParameterInfo(parameterInfo.Name ?? "", type, mandatory)
                            {
                                positionCSharp = parameterInfo.Position,
                                description = parameterInfo.GetCustomAttribute<DescriptionAttribute>()?.Description
                            }
                            ;
                            fctParams.Add(parameter);
                            if (parameter.positionCSharp != -1)
                            {
                                bool hasInParam = false;
                                foreach (string route in routes)
                                {
                                    if (ContainsParams(route, parameter))
                                    {
                                        hasInParam = true;
                                        break;
                                    }
                                }
                                if (!hasInParam)
                                {
                                    if (parameter.type != typeof(HttpContext) && !injected.ContainsKey(parameter.type))
                                    {
                                        hasBody = true;
                                    }
                                }
                            }
                        }

                        if (methodsToUse.Count == 0)
                        {
                            methodsToUse.Add(hasBody ? MethodType.Post : MethodType.Get);
                        }

                        if (addMcp)
                        {
                            List<RouterParameterInfo> _params = fctParams.FindAll(p => !injected.ContainsKey(p.type));
                            McpMiddleware.AddHttpMethod(new McpHttpMethod()
                            {
                                _params = fctParams,
                                _paramsFilter = _params,
                                method = method,
                                router = routerInstances[t]
                            });
                        }

                        foreach (string route in routes)
                        {
                            foreach (MethodType methodType in methodsToUse)
                            {

                                Dictionary<string, RouterParameterInfo> @params = fctParams.ToDictionary(p => p.name, p => p);
                                string urlPattern = route;
                                try
                                {
                                    Regex regex = transformPattern(urlPattern, @params, t, method);
                                    RouteInfo info = new RouteInfo(regex, methodType, method, routerInstances[t], parameters.Length);
                                    info.parameters = @params;


                                    if (!routesInfo.ContainsKey(info.UniqueKey))
                                    {
                                        if (config.PrintRoute)
                                            Console.WriteLine("Add http : " + info.ToString());
                                        routesInfo.Add(info.UniqueKey, info);
                                    }
                                    else
                                    {
                                        if (config.PrintRoute)
                                            Console.WriteLine("Add http : " + info.ToString());
                                        RouteInfo otherInfo = routesInfo[info.UniqueKey];
                                        result.Errors.Add(new RouteError(RouteErrorCode.RouteAlreadyExist, info.ToString() + " is already added from " + otherInfo.action.Name + " (" + otherInfo.action.DeclaringType?.Assembly.FullName + ")"));
                                    }
                                }
                                catch (Exception e)
                                {
                                    result.Errors.Add(new RouteError(RouteErrorCode.UnknowError, e));
                                }
                            }
                        }
                    }
                }
            }

            return result.ToGeneric();
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
        public static Regex PrepareUrl(string urlPattern, Dictionary<string, RouterParameterInfo> @params, Type t, MethodInfo methodInfo)
        {
            if (urlPattern.StartsWith("°") && urlPattern.EndsWith("°"))
            {
                return new Regex(urlPattern.Substring(1, urlPattern.Length - 2));
            }
            urlPattern = ReplaceParams(urlPattern, @params);
            urlPattern = ReplaceFunction(urlPattern, t);
            Regex regex = PrepareRegex(urlPattern);
            return regex;
        }
        public static string ReplaceFunction(string urlPattern, Type t)
        {
            MatchCollection matchingFct = new Regex("\\[[a-zA-Z0-9_]*?\\]").Matches(urlPattern);
            if (matchingFct.Count > 0)
            {
                foreach (Match match in matchingFct)
                {
                    string value = match.Value.Replace("[", "").Replace("]", "");
                    MethodInfo? method = t.GetMethod(value, BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (method == null)
                    {
                        Console.WriteLine("Can't find method " + value + " on " + t.FullName);
                        continue;
                    }
                    object? o = method.Invoke(routerInstances[t], Array.Empty<object>());
                    if (o != null)
                    {
                        urlPattern = urlPattern.Replace(match.Value, o.ToString());
                    }
                }
            }
            return urlPattern;
        }
        public static bool ContainsParams(string urlPattern, RouterParameterInfo param)
        {
            return new Regex("{" + param.name + "}").IsMatch(urlPattern);
        }
        public static string ReplaceParams(string urlPattern, Dictionary<string, RouterParameterInfo> @params)
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


        private static void LoadConfig()
        {
            if (!configLoaded)
            {
                configAction(config);
            }
        }

        public static async Task<RouterResolve?> Resolve(HttpContext context)
        {
            string url = context.Request.Path.ToString().ToLower();
            string method = context.Request.Method.ToLower();

            if (config.PrintRequest)
            {
                Console.WriteLine("trigger " + url + " " + method);
            }

            foreach (KeyValuePair<string, RouteInfo> routeInfo in routesInfo)
            {
                RouteInfo routerInfo = routeInfo.Value;

                if (routerInfo.method.ToString().ToLower() == method)
                {
                    Match match = routerInfo.pattern.Match(url);
                    if (match.Success)
                    {
                        RouterBody? body = null;
                        object?[] param = new object[routerInfo.nbParamsFunction];
                        foreach (RouterParameterInfo parameter in routerInfo.parameters.Values)
                        {
                            if (parameter.positionCSharp != -1)
                            {
                                if (parameter.positionUrl == -1)
                                {
                                    if (parameter.type == typeof(HttpContext))
                                    {
                                        param[parameter.positionCSharp] = context;
                                    }
                                    else
                                    {
                                        object? value = null;
                                        // check if dependancies injection
                                        if (injected.ContainsKey(parameter.type))
                                        {
                                            value = injected[parameter.type];
                                        }
                                        // check if body
                                        else
                                        {
                                            if (body == null)
                                            {
                                                body = new RouterBody(context);
                                                VoidWithRouteError resultTemp = await body.Parse();
                                                if (!resultTemp.Success)
                                                {
                                                    context.Response.StatusCode = 422;
                                                    await new Json(resultTemp).send(context, routerInfo.router);
                                                    return null;
                                                }
                                            }
                                            if (parameter.type == typeof(HttpFile))
                                            {
                                                value = body.GetFile(parameter.name);
                                            }
                                            else if (parameter.type == typeof(List<HttpFile>))
                                            {
                                                value = body.GetFiles(parameter.name);
                                            }
                                            else
                                            {
                                                ResultWithRouteError<object> bodyPart = body.GetData(parameter.type, parameter.name);
                                                if (!bodyPart.Success)
                                                {
                                                    context.Response.StatusCode = 422;
                                                    await new Json(bodyPart).send(context, routerInfo.router);
                                                    return null;
                                                }
                                                value = bodyPart.Result;
                                            }

                                        }

                                        // error
                                        if (value == null)
                                        {
                                            Console.WriteLine("ERRRRROOOOOR");
                                        }
                                        param[parameter.positionCSharp] = value;
                                    }
                                }
                                else
                                {
                                    string value = match.Groups[parameter.positionUrl + 1].Value;
                                    try
                                    {
                                        param[parameter.positionCSharp] = Convert.ChangeType(value, parameter.type);
                                    }
                                    catch (Exception)
                                    {
                                        param[parameter.positionCSharp] = Form.Tools.DefaultValue(parameter.type);
                                    }

                                }
                            }
                        }
                        if (config.PrintTrigger)
                        {
                            Console.WriteLine("trigger " + routerInfo.ToString());
                        }

                        return new RouterResolve(routerInfo, param);
                    }
                }
            }
            return null;
        }
        public static async Task OnRequest(HttpContext context, Func<Task> next)
        {
            RouterResolve? routerResolve = await Resolve(context);
            if (routerResolve != null)
            {
                await OnRequest(context, routerResolve);
                return;
            }
            await next();
        }

        public static async Task OnRequest(HttpContext context, RouterResolve routerResolve)
        {
            RouteInfo routerInfo = routerResolve.RouteInfo;
            object?[] param = routerResolve.Params;
            if (routerInfo.action.ReturnType == typeof(void))
            {
                routerInfo.action.Invoke(routerInfo.router, param);
                context.Response.StatusCode = 204;
            }
            else
            {
                object? o = routerInfo.action.Invoke(routerInfo.router, param);
                if (o is Task task)
                {
                    task.Wait();
                    if (!o.GetType().IsGenericType)
                    {
                        context.Response.StatusCode = 204;
                        return;
                    }
                    o = o.GetType().GetProperty("Result")?.GetValue(o);
                }

                if (o is IResponse response)
                {
                    await response.send(context, routerInfo.router);
                }
                else if (o is byte[] bytes)
                {
                    await new ByteResponse(bytes).send(context, routerInfo.router);
                }
                else if (o is string txt)
                {
                    await new TextResponse(txt).send(context, routerInfo.router);
                }
                else
                {
                    await new Json(o).send(context, routerInfo.router);
                }
            }
            return;
        }


    }
}
