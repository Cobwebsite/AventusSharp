using AventusSharp.Data;
using AventusSharp.Routes;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Response;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace CSharpToTypescript.Container
{
    internal class HttpRouterContainer : BaseContainer
    {
        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (type.AllInterfaces.ToList().Find(p => Tools.IsSameType<IRouter>(p)) != null)
            {
                if (Tools.ExportToTypesript(type, ProjectManager.Config.exportHttpRouteByDefault))
                {
                    result = new HttpRouterContainer(type);
                }
                return true;
            }
            return false;
        }

        private List<HttpRouteContainer> routes = new List<HttpRouteContainer>();
        private Dictionary<string, Func<string>> additionalFcts = new();
        public string? prefix;
        public HttpRouterContainer(INamedTypeSymbol type) : base(type)
        {
            foreach (ISymbol symbol in type.GetMembers())
            {

                if (symbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind != MethodKind.Constructor && !methodSymbol.IsExtern && !methodSymbol.IsStatic)
                {
                    HttpRouteContainer container = new HttpRouteContainer(methodSymbol, realType, this);
                    if (container.canBeAdded)
                    {
                        routes.Add(container);
                    }
                }
            }

            if (routes.Count == 0 && realType.BaseType == typeof(Router))
            {
                CanBeAdded = false;
                return;
            }

            ParentCheck();

            Attribute? prefixAttr = realType.GetCustomAttributes().FirstOrDefault(p => p is Prefix);
            if (prefixAttr is Prefix prefix)
            {
                this.prefix = prefix.txt;
            }
        }


        protected override string WriteAction()
        {
            List<string> result = new List<string>();
            if (ProjectManager.Config.useNamespace && Namespace.Length > 0)
            {
                AddIndent();
            }

            string documentation = GetDocumentation(type);
            if (documentation.Length > 0)
            {
                result.Add(documentation);
            }
            AddTxtOpen(GetAccessibilityExport(type) + GetAbstract() + "class " + GetTypeName(type, 0, true) + " " + GetExtension() + "{", result);
            if (prefix != null)
            {
                AddTxt("public override getPrefix(): string { return \"" + prefix + "\"; }", result);
            }

            ProjectConfigHttpRouter routerConfig = ProjectManager.Config.httpRouter;
            if (routerConfig.createRouter)
            {
                AddTxtOpen("public constructor(router?: Aventus.HttpRouter) {", result);
                AddTxt("super(router ?? new " + routerConfig.routerName + "());", result);
                AddTxtClose("}", result);
                string outputPath = Path.Combine(ProjectManager.Config.outputPath, routerConfig.routerName + ".lib.avt");
                string file = ProjectManager.Config.AbsoluteUrl(outputPath);
                this.addImport(file, routerConfig.routerName);
            }

            result.Add(GetContent());
            AddTxtClose("}", result);
            if (ProjectManager.Config.useNamespace && Namespace.Length > 0)
            {
                RemoveIndent();
            }

            return string.Join("\r\n", result);
        }

        private string GetContent()
        {
            List<string> result = new();
            Dictionary<string, string> functionNeeded = new Dictionary<string, string>();



            foreach (HttpRouteContainer route in routes)
            {
                result.Add(route.Write());

                foreach (KeyValuePair<string, Func<string>> fct in route.functionNeeded)
                {
                    if (!functionNeeded.ContainsKey(fct.Key))
                    {
                        functionNeeded.Add(fct.Key, fct.Value());
                    }
                }
            }
            foreach (KeyValuePair<string, string> fct in functionNeeded)
            {
                result.Add(fct.Value);
            }
            foreach (KeyValuePair<string, Func<string>> fct in additionalFcts)
            {
                if (!functionNeeded.ContainsKey(fct.Key))
                {
                    result.Add(fct.Value());
                }
            }
            return string.Join("\r\n\r\n", result);
        }

        private string GetAbstract()
        {
            if (type.IsAbstract)
            {
                return "abstract ";
            }
            return "";
        }

        private bool isParsingExtension = false;
        private string GetExtension()
        {
            string extend = "Aventus.HttpRoute";
            if (type.BaseType != null && type.BaseType.Name != "Object")
            {
                isParsingExtension = true;
                extend = GetTypeName(type.BaseType);
                isParsingExtension = false;
            }


            string txt = "extends " + extend + " ";

            return txt;
        }

        protected override string ParseGenericType(ITypeSymbol type, int depth, bool genericExtendsConstraint)
        {
            if (isParsingExtension)
            {
                if (type.TypeKind == TypeKind.Interface && type.AllInterfaces.ToList().Find(p => Tools.IsSameType<IStorable>(p)) != null)
                {
                    Type? realType = Type.GetType(type.ContainingNamespace.ToString() + "." + type.Name + ", " + type.ContainingAssembly.Name);
                    if (realType != null)
                    {
                        List<Type> types = realType.Assembly.GetTypes().Where(t => t.IsClass && t.GetInterfaces().Contains(realType)).ToList();
                        if (types.Count > 0)
                        {
                            Type finalType = types[0];
                            foreach (Type typeTemp in types)
                            {
                                if (finalType.IsSubclassOf(typeTemp))
                                {
                                    finalType = typeTemp;
                                }
                            }

                            ITypeSymbol? typeSymbol = ProjectManager.Compilation.GetTypeByMetadataName(finalType.FullName ?? "");
                            if (typeSymbol != null)
                            {
                                type = typeSymbol;
                            }
                        }
                    }
                    // Replace interface by real type
                }
            }
            return base.ParseGenericType(type, depth, genericExtendsConstraint);
        }
        private void ParentCheck()
        {
            if (type.IsAbstract)
            {
                return;
            }

            List<MethodInfo> methods = realType.GetMethods().ToList();
            Dictionary<string, MethodInfo> methodsByName = new();
            List<string> fcts = new List<string>();
            foreach (MethodInfo method in methods)
            {
                string fullName = method.DeclaringType?.Assembly.FullName ?? "";
                if (!method.IsPublic || fullName.StartsWith("System."))
                {
                    continue;
                }
                string urlPattern = "";
                Attribute? route = method.GetCustomAttributes().ToList().Find(p => p is AventusSharp.Routes.Attributes.Path);
                if (route != null)
                {
                    AventusSharp.Routes.Attributes.Path r = (AventusSharp.Routes.Attributes.Path)route;
                    urlPattern = r.pattern;
                }
                else
                {
                    urlPattern = AventusSharp.Routes.Tools.GetDefaultMethodUrl(method, ProjectManager.Config.httpRouter.defaultUrl);
                }
                ParseFunctions(urlPattern, fcts);
            }
            IRouter? routerTemp = (IRouter?)Activator.CreateInstance(realType);
            foreach (string fct in fcts)
            {
                MethodInfo? method = realType.GetMethod(fct, BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object? o = method?.Invoke(routerTemp, Array.Empty<object>());
                if (method == null)
                {
                    throw new Exception("Can't find method " + fct + " on " + realType.FullName);
                }
                if (o != null)
                {
                    Func<string> getFct = () =>
                    {
                        List<string> resultTemp = new();
                        IMethodSymbol? methodInfo = GetMethod(method);
                        string documentation = methodInfo != null ? GetDocumentation(methodInfo) : "";
                        if (documentation.Length > 0)
                        {
                            AddTxt(documentation, resultTemp);
                        }
                        else
                        {
                            AddTxt("/** @inheritdoc */", resultTemp);
                        }
                        AddTxtOpen("public " + method.Name + "() {", resultTemp);
                        AddTxt("return \"" + o.ToString() + "\";", resultTemp);
                        AddTxtClose("}", resultTemp);
                        return string.Join("\r\n", resultTemp);
                    };
                    additionalFcts.Add(method.Name, getFct);
                }
            }
        }
        private void ParseFunctions(string urlPattern, List<string> fcts)
        {
            MatchCollection matchingFct = new Regex("\\[.*?\\]").Matches(urlPattern);
            if (matchingFct.Count > 0)
            {
                foreach (Match match in matchingFct)
                {
                    string value = match.Value.Replace("[", "").Replace("]", "");
                    if (!fcts.Contains(value))
                    {
                        fcts.Add(value);
                    }
                }
            }
        }

        protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return applyReplacer(ProjectManager.Config.replacer.httpRouter, fullname, result);
        }
    }


    internal class HttpRouteContainer
    {
        private IMethodSymbol methodSymbol;
        public bool canBeAdded = false;
        public string name = "";
        private MethodInfo? _method;
        public MethodInfo method
        {
            get
            {
                if (_method == null) throw new Exception("Impossible");
                return _method;
            }
        }
        public List<string> httpMethods = new List<string>() { };
        private Type @class;
        public Dictionary<string, string> parametersBodyAndType = new();
        public Dictionary<string, string> parametersUrlAndType = new();
        public Dictionary<string, Func<string>> functionNeeded = new();
        private HttpRouterContainer parent;
        public string route = "";
        private List<string?> listReturns = new();
        private List<string?> listReturnsWithoutErrors = new();
        private Type? typeContainer;
        private string overrideTxt = "";

        public bool AvoidResultError
        {
            get
            {
                return typeContainer == typeof(Json);
            }
        }

        public HttpRouteContainer(IMethodSymbol methodSymbol, Type @class, HttpRouterContainer parent)
        {
            this.parent = parent;
            this.methodSymbol = methodSymbol;
            this.@class = @class;
            this.name = methodSymbol.Name;



            MethodInfo? methodTemp = Tools.GetMethodInfo(methodSymbol, @class);
            if (methodTemp == null || !methodTemp.IsPublic)
            {
                canBeAdded = false;
                return;
            }
            _method = methodTemp;
            canBeAdded = true;

            if (method.GetCustomAttribute(typeof(NoExport)) != null || method.GetCustomAttribute(typeof(NoRoute)) != null)
            {
                canBeAdded = false;
                return;
            }
            if (methodSymbol.IsOverride)
            {
                overrideTxt = "override ";
            }
            LoadHttpMethod(method);

            Attribute? attr = method.GetCustomAttribute(typeof(FctName));
            if (attr != null)
            {
                this.name = ((FctName)attr).name;
            }

            Dictionary<string, ParameterInfo> @params = method.GetParameters().ToDictionary(p => p.Name ?? "", p => p);
            foreach (KeyValuePair<string, ParameterInfo> pair in @params)
            {
                if (parametersUrlAndType.ContainsKey(pair.Key))
                {
                    continue;
                }

                foreach (var parameter in methodSymbol.Parameters)
                {
                    if (parameter.Name == pair.Key)
                    {
                        if (parameter.Type.ToString()?.StartsWith("Microsoft") == true)
                        {
                            continue;
                        }
                        if (Tools.HasAttribute<NoExport>(parameter))
                        {
                            continue;
                        }

                        if (parameter.Type.NullableAnnotation == NullableAnnotation.Annotated)
                        {
                            parametersBodyAndType.Add(pair.Key + "?", parent.GetTypeName(parameter.Type).Replace("?", ""));
                        }
                        else
                        {
                            parametersBodyAndType.Add(pair.Key, parent.GetTypeName(parameter.Type));
                        }
                    }
                }

            }

            Type returnType = method.ReturnType;
            if (returnType == typeof(Task))
            {
                returnType = typeof(void);
            }
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                returnType = returnType.GetGenericArguments()[0];
            }

            if (returnType != typeof(void))
            {
                bool needRecheck = false;
                if (returnType.GetInterfaces().Contains(typeof(IResponse)))
                {
                    typeContainer = returnType;
                }
                else if (returnType == typeof(IResponse))
                {
                    needRecheck = true;
                }
                else if (returnType == typeof(byte[]))
                {
                    typeContainer = typeof(ByteResponse);
                }
                else
                {
                    typeContainer = typeof(Json);
                }

                if (methodSymbol.DeclaringSyntaxReferences.Length > 0 && methodSymbol.DeclaringSyntaxReferences[0].GetSyntax() is MethodDeclarationSyntax methodSyntax)
                {
                    if (methodSyntax.Body == null)
                    {
                        canBeAdded = false;
                        return;
                    }
                    var temps = methodSyntax.Body.Statements;
                    foreach (var temp in temps)
                    {
                        LoadReturn(temp);
                    }
                    // can't load the type
                    if (listReturns.Count == 0)
                    {
                        AddTypeToListReturn(returnType);
                    }
                }
                if (needRecheck)
                {
                    if (listReturns.Count == 0 || listReturns.Count > 1)
                    {
                        canBeAdded = false;
                        Console.WriteLine("You need to define a single kind of reponse between Json, View, Dummy, etc");
                        return;
                    }
                    if (listReturns[0] == typeof(View).FullName)
                    {
                        canBeAdded = false;
                        //return;
                    }
                    typeContainer = Type.GetType(listReturns[0] + ", AventusSharp");
                    if (typeContainer == null)
                    {
                        typeContainer = typeof(Json);
                    }
                }

                if (typeContainer == null || typeContainer == typeof(View) || typeContainer == typeof(ViewDynamic))
                {
                    canBeAdded = false;
                }
            }


        }

        private void LoadHttpMethod(MethodInfo methodSymbol)
        {
            Dictionary<string, ParameterInfo> @params = method.GetParameters().ToDictionary(p => p.Name ?? "", p => p);
            List<object> attrs = methodSymbol.GetCustomAttributes(true).ToList();
            foreach (object attr in attrs)
            {
                if (attr is Get && !httpMethods.Contains("get"))
                {
                    httpMethods.Add("get");
                }
                else if (attr is Post && !httpMethods.Contains("post"))
                {
                    httpMethods.Add("post");
                }
                else if (attr is Put && !httpMethods.Contains("put"))
                {
                    httpMethods.Add("put");
                }
                else if (attr is Delete && !httpMethods.Contains("delete"))
                {
                    httpMethods.Add("delete");
                }
                else if (attr is Options && !httpMethods.Contains("options"))
                {
                    httpMethods.Add("options");
                }
                else if (attr is AventusSharp.Routes.Attributes.Path pathAttr)
                {
                    ParseRoute(pathAttr.pattern, @params);
                }
            }
            if (route == "")
            {
                string defaultName = AventusSharp.Routes.Tools.GetDefaultMethodUrl(method, ProjectManager.Config.httpRouter.defaultUrl);
                ParseRoute(defaultName, @params);
            }
            bool hasBody = false;
            foreach (var parameter in this.methodSymbol.Parameters)
            {
                if (parameter.Type.ToString()?.StartsWith("Microsoft") == true)
                {
                    continue;
                }
                if (Tools.HasAttribute<NoExport>(parameter))
                {
                    continue;
                }
                bool hasInParam = new Regex("{.*?}").IsMatch(route);
                if (!hasInParam)
                {
                    hasBody = true;
                }
            }

            if (httpMethods.Count == 0)
            {
                httpMethods.Add(hasBody ? "post" : "get");
            }

        }


        private void ParseRoute(string? txtRoute, Dictionary<string, ParameterInfo> @params)
        {
            if (txtRoute != null)
            {
                txtRoute = ParseParams(txtRoute, @params);
                txtRoute = ParseFunctions(txtRoute);
                this.route = txtRoute;
                return;
            }

        }
        public string ParseParams(string urlPattern, Dictionary<string, ParameterInfo> @params)
        {
            MatchCollection matching = new Regex("{.*?}").Matches(urlPattern);
            foreach (Match match in matching)
            {
                string value = match.Value.Replace("{", "").Replace("}", "");
                if (parametersUrlAndType.ContainsKey(value))
                {
                    continue;
                }
                if (@params.ContainsKey(value))
                {
                    if (@params[value].ParameterType == typeof(int))
                    {
                        parametersUrlAndType.Add(value, "number");
                    }
                    else if (@params[value].ParameterType == typeof(string))
                    {
                        parametersUrlAndType.Add(value, "string");
                    }
                    else
                    {
                        parametersUrlAndType.Add(value, "any");
                    }
                }
                else
                {
                    parametersUrlAndType.Add(value, "any");
                }
                urlPattern = urlPattern.Replace(match.Value, "${" + value + "}");
            }
            return urlPattern;
        }
        public string ParseFunctions(string urlPattern)
        {
            MatchCollection matchingFct = new Regex("\\[.*?\\]").Matches(urlPattern);
            if (matchingFct.Count > 0)
            {
                foreach (Match match in matchingFct)
                {
                    string value = match.Value.Replace("[", "").Replace("]", "");
                    MethodInfo? methodTemp = @class.GetMethod(value, BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (methodTemp == null)
                    {
                        canBeAdded = false;
                        return urlPattern;
                    }
                    if (!functionNeeded.ContainsKey(methodTemp.Name))
                    {
                        if (@class.IsAbstract)
                        {
                            Func<string> getTxt = () =>
                            {
                                return parent.GetIndentedText("public abstract " + methodTemp.Name + "(): string;");
                            };
                            functionNeeded.Add(methodTemp.Name, getTxt);
                        }
                        else
                        {
                            IRouter? routerTemp = (IRouter?)Activator.CreateInstance(@class);
                            object? o = methodTemp.Invoke(routerTemp, Array.Empty<object>());
                            if (o != null)
                            {
                                Func<string> getTxt = () =>
                                {
                                    List<string> resultTemp = new();
                                    parent.AddTxtOpen("public " + methodTemp.Name + "() {", resultTemp);
                                    parent.AddTxt("return \"" + o.ToString() + "\";", resultTemp);
                                    parent.AddTxtClose("}", resultTemp);
                                    return string.Join("\r\n", resultTemp);
                                };

                                functionNeeded.Add(methodTemp.Name, getTxt);
                            }
                        }
                    }

                    urlPattern = urlPattern.Replace(match.Value, "${this." + value + "()}");
                }
            }
            return urlPattern;
        }
        private void AddTypeToListReturn(ISymbol type)
        {
            if (type is INamedTypeSymbol namedType)
            {
                Type? type2 = Tools.GetCompiledType(namedType);
                if (type2 != null && AvoidResultError)
                {
                    if (Tools.IsSubclass(typeof(ResultWithError<,>), type2))
                    {
                        listReturns.Add(parent.GetTypeName(namedType.TypeArguments[namedType.TypeArguments.Length - 1]));
                        return;
                    }
                    else if (Tools.IsSubclass(typeof(VoidWithError<>), type2))
                    {
                        return;
                    }
                }
            }
            listReturns.Add(parent.GetTypeName(type));
        }
        private void AddTypeToListReturn(Type type)
        {
            ITypeSymbol? typeSymbol = ProjectManager.Compilation.GetTypeByMetadataName(type.FullName ?? "");
            if (typeSymbol is INamedTypeSymbol namedType && AvoidResultError)
            {
                if (Tools.IsSubclass(typeof(ResultWithError<,>), type))
                {
                    listReturns.Add(parent.GetTypeName(namedType.TypeArguments[namedType.TypeArguments.Length - 1]));
                    return;
                }
                else if (Tools.IsSubclass(typeof(VoidWithError<>), type))
                {
                    return;
                }
                listReturns.Add(parent.GetTypeName(typeSymbol));
            }
        }
        private void LoadReturn(SyntaxNode node)
        {
            if (node is ReturnStatementSyntax returnStatement)
            {
                if (returnStatement.Expression != null)
                {
                    if (returnStatement.Expression is BinaryExpressionSyntax)
                    {
                        listReturns.Add("bool");
                        return;
                    }
                    SymbolInfo temp = ProjectManager.Compilation.GetSemanticModel(node.SyntaxTree).GetSymbolInfo(returnStatement.Expression);
                    bool isAwait = false;
                    if (returnStatement.Expression is AwaitExpressionSyntax awaitExpressionSyntax)
                    {
                        temp = ProjectManager.Compilation.GetSemanticModel(node.SyntaxTree).GetSymbolInfo(awaitExpressionSyntax.Expression);
                        isAwait = true;
                    }
                    if (temp.Symbol is ILocalSymbol localSymbol)
                    {
                        AddTypeToListReturn(localSymbol.Type);
                    }
                    else if (temp.Symbol is IMethodSymbol methodSymbol)
                    {
                        if (methodSymbol.MethodKind == MethodKind.Constructor && returnStatement.Expression is ObjectCreationExpressionSyntax cst)
                        {
                            string? name = methodSymbol.ContainingType.ToString();
                            if (name == typeof(Json).FullName)
                            {
                                GetTypeFirstParameterType(cst?.ArgumentList?.Arguments[0]);
                            }
                            else
                            {
                                AddTypeToListReturn(methodSymbol.ContainingType);
                            }
                        }
                        else
                        {
                            if (isAwait)
                            {
                                if (methodSymbol.ReturnType is INamedTypeSymbol named)
                                {
                                    ImmutableArray<ITypeSymbol> arguments = named.TypeArguments;
                                    if (arguments.Length == 1)
                                    {
                                        AddTypeToListReturn(arguments[0]);
                                    }
                                }
                            }
                            else
                            {
                                AddTypeToListReturn(methodSymbol.ReturnType);
                            }
                        }
                    }
                    else if (temp.Symbol is IPropertySymbol propertySymbol)
                    {

                        AddTypeToListReturn(propertySymbol.Type);
                    }
                    else
                    {
                        Console.WriteLine("return type is " + temp.Symbol?.GetType());
                    }
                }
            }
            else
            {
                foreach (var child in node.ChildNodes())
                {
                    LoadReturn(child);
                }
            }
        }
        private void GetTypeFirstParameterType(ArgumentSyntax? argument)
        {
            if (argument == null)
            {
                return;
            }
            if (argument.Expression is LiteralExpressionSyntax literal)
            {
                listReturns.Add(literal.ToString());
                return;
            }
            SymbolInfo argumentSymbol = ProjectManager.Compilation.GetSemanticModel(argument.SyntaxTree).GetSymbolInfo(argument.Expression);
            if (argumentSymbol.Symbol is ILocalSymbol localSymbol)
            {
                AddTypeToListReturn(localSymbol.Type);
            }
            else if (argumentSymbol.Symbol is IMethodSymbol methodSymbol)
            {
                if (methodSymbol.MethodKind == MethodKind.Constructor)
                {
                    AddTypeToListReturn(methodSymbol.ContainingType);
                }
                else
                {
                    AddTypeToListReturn(methodSymbol.ReturnType);
                }
            }
            else
            {
                Console.WriteLine("return type is " + argumentSymbol.Symbol?.GetType());
            }
        }

        public string Write()
        {
            List<string> result = new();
            string bodyKey = "body";
            if (parametersBodyAndType.Count > 0)
            {
                int i = 0;
                while (parametersUrlAndType.ContainsKey(bodyKey))
                {
                    bodyKey = "body" + i;
                    i++;
                }

                parametersUrlAndType[bodyKey] = "{ " + string.Join(", ", parametersBodyAndType.Select(p => p.Key + ": " + p.Value)) + " } | FormData";
            }

            string @params = string.Join(", ", parametersUrlAndType.Select(p => p.Key + ": " + p.Value));

            string fctDesc = BaseContainer.GetAccessibility(methodSymbol) + overrideTxt + "async " + name + "(" + @params + "): $resultType {";
            string request = "const request = new Aventus.HttpRequest(`${this.getPrefix()}" + route + "`, Aventus.HttpMethod." + this.httpMethods[0].ToUpper() + ");";
            string body = "";
            string resultWithErrorType = parent.GetAventusTypeName("AventusSharp.Tools.ResultWithError");
            string voidWithErrorType = parent.GetAventusTypeName("AventusSharp.Tools.VoidWithError");
            if (parametersBodyAndType.Count > 0)
            {
                body = "request.setBody(" + bodyKey + ");";
            }
            string returnTxt = "";
            string typeTxt = "";
            if (typeContainer == null)
            {
                returnTxt = "return await request.queryVoid(this.router);";
                fctDesc = fctDesc.Replace("$resultType", "Promise<" + voidWithErrorType + ">");
            }
            else if (typeContainer == typeof(Json))
            {
                if (listReturns.Count == 0)
                {
                    returnTxt = "return await request.queryVoid(this.router);";
                    fctDesc = fctDesc.Replace("$resultType", "Promise<" + voidWithErrorType + ">");
                }
                else if (listReturns.Count == 1 && new Regex("Aventus\\.VoidWithError(<|$)").IsMatch(listReturns[0] ?? ""))
                {
                    returnTxt = "return await request.queryVoid(this.router);";
                    fctDesc = fctDesc.Replace("$resultType", "Promise<" + voidWithErrorType + ">");
                }
                else
                {
                    List<string> realTypes = new List<string>();
                    foreach (string? itemReturn in listReturns)
                    {
                        if (itemReturn == null) continue;
                        string item = itemReturn;
                        item = new Regex("Aventus\\.GenericResultWithError<(.*?)>").Replace(itemReturn ?? "", "$1");
                        if (item.EndsWith("?"))
                        {
                            item = item.Substring(0, item.Length - 1);
                            realTypes.Add(item);
                            if (!realTypes.Contains("undefined"))
                            {
                                realTypes.Add("undefined");
                            }
                        }
                        else
                        {
                            realTypes.Add(item);
                        }
                    }

                    string typeReturn = string.Join(" | ", realTypes);
                    typeTxt = "type TypeResult = " + typeReturn + ";";
                    fctDesc = fctDesc.Replace("$resultType", "Promise<" + resultWithErrorType + "<" + typeReturn + ">>");
                    returnTxt = "return await request.queryJSON<TypeResult>(this.router);";
                }

            }
            else if (typeContainer == typeof(ByteResponse))
            {
                fctDesc = fctDesc.Replace("$resultType", "Promise<" + resultWithErrorType + "<Blob>>");
                returnTxt = "return await request.queryBlob(this.router);";
            }
            else if (typeContainer == typeof(TextResponse))
            {
                fctDesc = fctDesc.Replace("$resultType", "Promise<" + resultWithErrorType + "<string>>");
                returnTxt = "return await request.queryTxt(this.router);";
            }
            else
            {
                fctDesc = fctDesc.Replace("$resultType", "Promise<" + resultWithErrorType + "<string>>");
                returnTxt = "return await request.queryTxt(this.router);";
            }



            parent.AddTxt("@BindThis()", result);
            parent.AddTxtOpen(fctDesc, result);
            parent.AddTxt(request, result);
            if (!string.IsNullOrEmpty(body))
            {
                parent.AddTxt(body, result);
            }
            if (!string.IsNullOrEmpty(typeTxt))
            {
                parent.AddTxt(typeTxt, result);
            }
            if (!string.IsNullOrEmpty(returnTxt))
            {
                parent.AddTxt(returnTxt, result);
            }
            parent.AddTxtClose("}", result);


            return string.Join("\r\n", result);
        }
    }
}
