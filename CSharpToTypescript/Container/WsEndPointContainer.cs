using AventusSharp.WebSocket;
using Microsoft.CodeAnalysis;

namespace CSharpToTypescript.Container
{
    internal class WsEndPointContainer : BaseClassContainer
    {
        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (type.AllInterfaces.ToList().Find(p => Tools.IsSameType<IWsEndPoint>(p)) != null)
            {
                if (Tools.ExportToTypesript(type, ProjectManager.Config.exportWsEndPointByDefault))
                {
                    result = new WsEndPointContainer(type);
                }
                return true;
            }
            return false;
        }


        private string className = "";
        private string fullClassName = "";
        private string path = "";
        private new Type realType;
        private bool isMain = false;
        private bool isParentWsEndPoint = true;

        public WsEndPointContainer(INamedTypeSymbol type) : base(type)
        {
            string fullName = type.ContainingNamespace.ToString() + "." + type.Name;
            if (type.IsGenericType)
            {
                fullName += "`" + type.TypeParameters.Length;
            }
            Type? realType = Tools.GetTypeFromFullName(fullName);
            if (realType == null)
            {
                throw new Exception("something went wrong on ws end point");
            }
            this.realType = realType;
            isParentWsEndPoint = (realType.BaseType == typeof(WsEndPoint));
            if (realType.IsAbstract) return;

            WsEndPoint? endPoint = (WsEndPoint?)Activator.CreateInstance(realType);
            if (endPoint == null)
            {
                throw new Exception("something went wrong on ws end point");
            }
            path = endPoint.Path;
            isMain = endPoint.Main();
        }


        protected override string WriteAction()
        {

            List<string> result = new List<string>();
            if (ProjectManager.Config.useNamespace && Namespace.Length > 0)
            {
                AddIndent();
            }

            fullClassName = GetTypeName(type, 0, true);
            className = fullClassName.Split(".").Last();

            string documentation = GetDocumentation(type);
            if (documentation.Length > 0)
            {
                result.Add(documentation);
            }

            AddTxtOpen(GetAccessibilityExport(type) + GetAbstract() + "class " + className + " extends " + ProjectManager.Config.wsEndpoint.parent + " {", result);
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
            if (!isInterface && type.IsAbstract)
            {
                return "abstract ";
            }
            return "";
        }

        private string GetContent()
        {
            List<string> result = new();
            if (!realType.IsAbstract)
            {
                AddTxt("", result);
                AddTxt("/**", result);
                AddTxt(" * Create a singleton", result);
                AddTxt(" */", result);
                AddTxtOpen("public static override getInstance(): " + className + " {", result);
                AddTxt("return Aventus.Instance.get(" + className + ");", result);
                AddTxtClose("}", result);
                AddTxt("", result);

                List<string> options = new();
                ProjectConfigWsEndpoint config = ProjectManager.Config.wsEndpoint;
                if (config.host != null)
                {
                    options.Add("options.host = \"" + config.host + "\";");
                }
                if (config.port != null)
                {
                    options.Add("options.port = " + config.port + ";");
                }
                if (config.useHttps != null)
                {
                    options.Add("options.useHttps = " + config.useHttps + ";");
                }

                if(options.Count > 0)
                {
                    AddTxtOpen("protected override configure(options: AventusSharp.WebSocket.ConnectionOptions): AventusSharp.WebSocket.ConnectionOptions {", result);
                    foreach(string option in options)
                    {
                        AddTxt(option, result);
                    }
                    AddTxt("return options;", result);
                    AddTxtClose("}", result);
                }

                AddTxtOpen("protected override get path(): string {", result);
                AddTxt("return \"" + path + "\";", result);
                AddTxtClose("}", result);
            }
            return string.Join("\r\n", result);
        }

        protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return applyReplacer(ProjectManager.Config.replacer.wsEndPoint, fullname, result);
        }
    }

}
