using AventusSharp.WebSocket;
using AventusSharp.WebSocket.Attributes;
using AventusSharp.WebSocket.Event;
using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using Path = AventusSharp.WebSocket.Attributes.Path;

namespace CSharpToTypescript.Container
{
    internal class WsEventContainer : BaseContainer
    {
        public static Dictionary<INamedTypeSymbol, WsEventContainer> CreatedEvents = [];
        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (type.AllInterfaces.ToList().Find(p => Tools.IsSameType<IWebSocketEvent>(p)) != null)
            {
                if (Tools.ExportToTypesript(type, ProjectManager.Config.exportWsEventByDefault))
                {
                    if (!CreatedEvents.ContainsKey(type))
                    {
                        result = new WsEventContainer(type, fileName);
                    }
                    else
                    {
                        result = CreatedEvents[type];
                    }
                }
                return true;
            }
            return false;
        }


        public string typescriptPath = "";
        public string eventPath = "";
        public string fileName = "";
        private bool? listenOnBoot = null;
        public Dictionary<string, Func<string>> functionNeeded = new();
        public List<string> fctsConstructor = new();
        public Type? endPoint;

        public WsEventContainer(INamedTypeSymbol type, string? fileName) : base(type)
        {
            CreatedEvents.Add(type, this);
            this.fileName = fileName ?? FileToWrite.GetFileName(type) ?? throw new Exception("Impossible: no filename");
            string fullName = type.ContainingNamespace.ToString() + "." + type.Name;
            if (type.IsGenericType)
            {
                fullName += "`" + type.TypeParameters.Length;
            }
            Type? realType = Tools.GetTypeFromFullName(fullName);
            if (realType == null)
            {
                throw new Exception("something went wrong");
            }
            this.realType = realType;
            this.LoadBody();
            this.ParseAttributes();
        }

        private void LoadBody()
        {
            Type type = realType;
            while (type.BaseType != null)
            {
                Type newType = type.BaseType;
                if (newType.IsGenericType && newType.GetGenericTypeDefinition() == typeof(WsEvent<>))
                {
                    Type body = newType.GetGenericArguments()[0];
                    if (body.IsNested)
                    {
                        ITypeSymbol? typeSymbol = ProjectManager.Compilation.GetTypeByMetadataName(body.FullName ?? "");
                        if (typeSymbol is INamedTypeSymbol namedType)
                        {
                            FileToWrite.AddBaseContainer(new NormalClassContainer(namedType), fileName);
                        }
                    }
                }

                type = newType;
            }
        }

        private void ParseAttributes()
        {
            IEnumerable<Attribute> attrs = realType.GetCustomAttributes();
            foreach (Attribute attr in attrs)
            {
                if (attr is EndPoint endPointAttr)
                {
                    endPoint = endPointAttr.endpoint;
                }
                else if (attr is Path pathAttr)
                {
                    this.eventPath = pathAttr.pattern;
                }
                else if (attr is ListenOnBoot listenOnBootAttr)
                {
                    listenOnBoot = listenOnBootAttr.listen;
                }
            }
            if (this.eventPath == "")
            {
                this.eventPath = realType.FullName ?? "";
            }
            List<string> fcts = new List<string>();
            eventPath = ParseFunctions(eventPath);
            string prefix = ProjectManager.Config.wsEndpoint.prefix;
            if (prefix != "")
            {
                eventPath = prefix + eventPath;
            }
        }

        private string ParseFunctions(string urlPattern)
        {
            MatchCollection matchingFct = new Regex("\\[.*?\\]").Matches(urlPattern);
            if (matchingFct.Count > 0)
            {
                foreach (Match match in matchingFct)
                {
                    string value = match.Value.Replace("[", "").Replace("]", "");
                    MethodInfo? methodTemp = realType.GetMethod(value, BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (methodTemp == null || realType.IsGenericTypeDefinition)
                    {
                        // inject the method though constructor
                        if (!fctsConstructor.Contains(value))
                        {
                            fctsConstructor.Add(value);
                        }
                    }
                    else if (!functionNeeded.ContainsKey(methodTemp.Name))
                    {
                        if (realType.IsAbstract)
                        {
                            Func<string> getTxt = () =>
                            {
                                return GetIndentedText("public abstract " + methodTemp.Name + "(): string;");
                            };
                            functionNeeded.Add(methodTemp.Name, getTxt);
                        }
                        else
                        {
                            IWebSocketEvent? routerTemp = (IWebSocketEvent?)Activator.CreateInstance(realType);
                            object? o = methodTemp.Invoke(routerTemp, Array.Empty<object>());
                            if (o != null)
                            {
                                Func<string> getTxt = () =>
                                {
                                    List<string> resultTemp = new();
                                    AddTxtOpen("public " + methodTemp.Name + "() {", resultTemp);
                                    AddTxt("return \"" + o.ToString() + "\";", resultTemp);
                                    AddTxtClose("}", resultTemp);
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
            result.Add(GetContent());
            AddTxtClose("}", result);
            if (ProjectManager.Config.useNamespace && Namespace.Length > 0)
            {
                RemoveIndent();
            }

            return string.Join("\r\n", result);
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
            string extend = "AventusSharp.Socket.WsEvent";
            if (type.BaseType != null && type.BaseType.Name != "Object")
            {
                isParsingExtension = true;
                extend = GetTypeName(type.BaseType);
                isParsingExtension = false;
            }


            string txt = "extends " + extend + " ";

            return txt;
        }

        private string GetContent()
        {
            List<string> result = new();

            AddTxt("/**", result);
            AddTxt(" * @inheritdoc", result);
            AddTxt(" */", result);
            AddTxtOpen("protected override path(): string {", result);
            AddTxt("return `${this.getPrefix()}" + eventPath + "`;", result);
            AddTxtClose("}", result);

            if (fctsConstructor.Count > 0 || endPoint != null)
            {
                string endpointName = GetTypeName(typeof(WsEndPoint));
                string mark = endPoint != null ? "?" : "";
                string constructorTxt = "public constructor(endpoint"+ mark + ": " + endpointName + ", getPrefix?: () => string";
                foreach (string fctToInject in fctsConstructor)
                {
                    constructorTxt += ", " + fctToInject + "?: () => string";
                    AddTxt("public " + fctToInject + ": () => string;", result);
                }

                constructorTxt += ") {";
                AddTxtOpen(constructorTxt, result);
                if (endPoint != null)
                {
                    string endPointType = GetTypeName(endPoint);
                    AddTxt("super(endpoint ?? " + endPointType + ".getInstance(), getPrefix);", result);
                }
                else
                {
                    AddTxt("super(endpoint, getPrefix);", result);
                }
                foreach (string fctToInject in fctsConstructor)
                {
                    AddTxt("this." + fctToInject + " = "+fctToInject+" ?? (() => \"\")", result);
                }
                AddTxtClose("}", result);
            }

            if (listenOnBoot != null)
            {
                string trueTxt = listenOnBoot == true ? "true" : "false";
                AddTxt("", result);
                AddTxt("/**", result);
                AddTxt(" * @inheritdoc", result);
                AddTxt(" */", result);
                AddTxtOpen("protected override listenOnBoot(): boolean {", result);
                AddTxt("return " + trueTxt + ";", result);
                AddTxtClose("}", result);
            }

            foreach (KeyValuePair<string, Func<string>> fct in functionNeeded)
            {
                result.Add(fct.Value());
            }

            return string.Join("\r\n", result);
        }

        protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return applyReplacer(ProjectManager.Config.replacer.wsEvent, fullname, result);
        }
    }
}
