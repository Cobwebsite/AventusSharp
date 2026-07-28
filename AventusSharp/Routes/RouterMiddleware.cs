using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Request;
using AventusSharp.Routes.Response;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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

        private static AsyncLocal<HttpContext?> _contextScope = new();
        public static HttpContext? ContextScope
        {
            get => _contextScope.Value;
            internal set => _contextScope.Value = value;
        }

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
                    List<Middleware> middlewaresClass = new List<Middleware>();
                    foreach (Attribute routeAttribute in routeAttributes)
                    {
                        if (routeAttribute is Prefix prefixAttr)
                        {
                            prefix = prefixAttr.txt;
                        }
                        else if (routeAttribute is Middleware middleware)
                        {
                            middlewaresClass.Add(middleware);
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
                        List<Middleware> middlewares = middlewaresClass.ToList();
                        bool canUse = true;
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
                            else if (methodAttribute is Middleware middleware)
                            {
                                middlewares.Add(middleware);
                            }
                        }
                        if (!canUse) continue;

                        List<RouterParameterInfo> fctParams = new List<RouterParameterInfo>();
                        ParameterInfo[] parameters = method.GetParameters();

                        if (routes.Count == 0)
                        {

                            string defaultName = Tools.GetDefaultMethodUrl(method, config.defaultUrl, prefix);
                            routes.Add(defaultName);
                        }


                        bool hasBody = false;
                        foreach (ParameterInfo parameterInfo in parameters)
                        {

                            RouterParameterInfo parameter = new RouterParameterInfo(parameterInfo.Name ?? "", parameterInfo.ParameterType)
                            {
                                positionCSharp = parameterInfo.Position,
                                optional = parameterInfo.IsOptional
                            };

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

                        foreach (string route in routes)
                        {
                            foreach (MethodType methodType in methodsToUse)
                            {

                                Dictionary<string, RouterParameterInfo> @params = fctParams.ToDictionary(p => p.name, p => p);
                                string urlPattern = route;
                                try
                                {
                                    Regex regex = transformPattern(urlPattern, @params, t, method);
                                    RouteInfo info = new RouteInfo(regex, methodType, method, routerInstances[t], parameters.Length, middlewares, urlPattern);
                                    info.parameters = @params;


                                    if (!routesInfo.ContainsKey(info.UniqueKey))
                                    {
                                        if (config.PrintRoute)
                                            AventusLogger.Instance.LogInformation("Add http : " + info.ToString());
                                        routesInfo.Add(info.UniqueKey, info);
                                    }
                                    else
                                    {
                                        if (config.PrintRoute)
                                            AventusLogger.Instance.LogInformation("Add http : " + info.ToString());
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

        public static void PrintForExport()
        {
            if (routesInfo.Count == 0) return;

            Console.WriteLine("--- Routes HTTP ---");
            List<RouteExposeHttp> expose = new List<RouteExposeHttp>();
            foreach (KeyValuePair<string, RouteInfo> routeInfo in routesInfo)
            {
                expose.Add(new RouteExposeHttp()
                {
                    Method = routeInfo.Value.method,
                    BaseUrl = routeInfo.Value.baseUrl,
                    Pattern = routeInfo.Value.pattern.ToString(),
                    MethodName = routeInfo.Value.action.Name,
                    ClassName = routeInfo.Value.action.ReflectedType!.FullName!,
                    Params = routeInfo.Value.parameters.Select(p => p.Value.type.FullName!).ToList()
                });

            }
            Console.WriteLine(JsonConvert.SerializeObject(expose));
            Console.WriteLine("-------------------");
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
                AventusLogger.Instance.LogError("Can't create " + typeof(U));
        }
        public static Regex PrepareUrl(string urlPattern, Dictionary<string, RouterParameterInfo> @params, Type t, MethodInfo methodInfo)
        {
            if (urlPattern.StartsWith("°") && urlPattern.EndsWith("°"))
            {
                return new Regex(urlPattern.Substring(1, urlPattern.Length - 2));
            }
            urlPattern = ReplaceParams(urlPattern, @params);
            urlPattern = ReplaceFunction(urlPattern, t);
            if (config.transformPath != null)
            {
                urlPattern = config.transformPath(urlPattern, @params, t, methodInfo);
            }
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
                        AventusLogger.Instance.LogError("Can't find method " + value + " on " + t.FullName);
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
            return new Regex(urlPattern, RegexOptions.IgnoreCase);
        }


        private static void LoadConfig()
        {
            if (!configLoaded)
            {
                configAction(config);
                configLoaded = true;
            }
        }

        public static async Task<RouterResolve?> Resolve(HttpContext context)
        {
            if (context.Items.ContainsKey("routerResolve") && context.Items["routerResolve"] is RouterResolve router)
            {
                return router;
            }

            string url = context.Request.Path.ToString();

            foreach (KeyValuePair<string, RouteInfo> routeInfo in routesInfo)
            {
                RouteInfo routerInfo = routeInfo.Value;

                if (routerInfo.method.ToString().ToLower() == context.Request.Method.ToLower())
                {
                    Match match = routerInfo.pattern.Match(url);
                    if (match.Success)
                    {
                        if (config.PrintTrigger)
                        {
                            AventusLogger.Instance.LogInformation("trigger " + routeInfo.Value.ToString());
                        }
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
                                            value = context.RequestServices.GetService(parameter.type);
                                            if (value == null)
                                            {
                                                if (body == null)
                                                {
                                                    body = new RouterBody(context);
                                                    VoidWithRouteError resultTemp = await body.Parse();
                                                    if (!resultTemp.Success)
                                                    {
                                                        context.Response.StatusCode = 422;
                                                        await new Json(resultTemp, 422).send(context, routerInfo.router);
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
                                                    ResultWithRouteError<object> bodyPart = body.GetData(parameter.type, parameter.name, parameter.optional);
                                                    if (!bodyPart.Success)
                                                    {
                                                        context.Response.StatusCode = 422;
                                                        await new Json(bodyPart, 422).send(context, routerInfo.router);
                                                        return null;
                                                    }
                                                    value = bodyPart.Result;
                                                }
                                            }
                                        }

                                        // error
                                        if (value == null && !parameter.optional)
                                        {
                                            AventusLogger.Instance.LogError("Can't find the parameter " + parameter.name + " for http request " + url + "(" + context.Request.Method + ")");
                                        }
                                        param[parameter.positionCSharp] = value;
                                    }
                                }
                                else
                                {
                                    string value = match.Groups[parameter.positionUrl + 1].Value;
                                    try
                                    {
                                        param[parameter.positionCSharp] = System.Convert.ChangeType(value, parameter.type);
                                    }
                                    catch (Exception)
                                    {
                                        param[parameter.positionCSharp] = Form.Tools.DefaultValue(parameter.type);
                                    }

                                }
                            }
                        }


                        RouterResolve routerResolve = new RouterResolve(routerInfo, param);
                        context.Items["routerResolve"] = routerResolve;
                        return routerResolve;
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
            bool canContinue = true;
            ContextScope = context;
            try
            {
                foreach (Middleware middleware in routerInfo.middlewares)
                {
                    if (!canContinue) return;
                    canContinue = false;
                    await middleware.Run(context, routerInfo, () =>
                    {
                        return Task.Run(() =>
                        {
                            canContinue = true;
                        });
                    });
                }
                if (!canContinue)
                {
                    ContextScope = null;
                    return;
                }

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
                        await (dynamic)task;
                        if (!routerInfo.action.ReturnType.IsGenericType)
                        {
                            context.Response.StatusCode = 204;
                            ContextScope = null;
                            return;
                        }
                        o = ((dynamic)task).Result;
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
            }
            catch (Exception exception)
            {
                Exception routeException =
                    exception is TargetInvocationException invocationException &&
                    invocationException.InnerException is Exception innerException
                        ? innerException
                        : exception;
                GenericError routeError =
                    routeException is AventusException aventusException
                        ? aventusException.Error
                        : new RouteError(
                            RouteErrorCode.UnknowError,
                            routeException);
                int code = context.Response.StatusCode >= 400
                    ? context.Response.StatusCode
                    : 500;
                VoidWithError error = new() { Errors = [routeError] };
                await new Json(error, code).send(context);
            }
            ContextScope = null;
        }


    }
}
