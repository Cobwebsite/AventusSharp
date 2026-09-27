using AventusSharp.SSE;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace CSharpToTypescript.Container
{
    internal class SSEEndPointContainer : BaseClassContainer
    {
        private static readonly List<(INamedTypeSymbol Type, bool Main)> endpoints = [];

        internal static INamedTypeSymbol? GetDefaultEndpoint()
        {
            var main = endpoints.Where(endpoint => endpoint.Main).ToList();
            if (main.Count > 1)
                throw new InvalidOperationException("Multiple main SSE endpoints cannot be generated.");
            if (main.Count == 1) return main[0].Type;
            if (endpoints.Count == 1) return endpoints[0].Type;
            return null;
        }
        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (type.AllInterfaces.ToList().Find(p => Tools.IsSameType<ISSEEndPoint>(p)) != null)
            {
                if (type.ContainingAssembly.Name == typeof(ISSEEndPoint).Assembly.GetName().Name)
                    return true;
                if (Tools.ExportToTypesript(type, ProjectManager.Config.exportSseEndPointByDefault))
                {
                    result = new SSEEndPointContainer(type);
                }
                return true;
            }
            return false;
        }


        private string className = "";
        private string fullClassName = "";
        private string path = "";
        private new Type realType;
        private bool isParentSSEEndPoint = true;

        public SSEEndPointContainer(INamedTypeSymbol type) : base(type)
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
            isParentSSEEndPoint = (realType.BaseType == typeof(SSEEndPoint));
            if (realType.IsAbstract) return;

            SSEEndPoint? endPoint = (SSEEndPoint?)Activator.CreateInstance(realType);
            if (endPoint == null)
            {
                throw new Exception("something went wrong on ws end point");
            }
            path = endPoint.Path;
            endpoints.Add((type, endPoint.Main()));
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

            string parent = GetAventusTypeName(ProjectManager.Config.sseEndpoint.parent);
            if (!isParentSSEEndPoint && type.BaseType != null)
                parent = GetTypeName(type.BaseType);
            AddTxtOpen(GetAccessibilityExport(type) + GetAbstract() + "class " + className + " extends " + parent + " {", result);
            result.Add(GetContent());
            AddTxtClose("}", result);
            if (ProjectManager.Config.useNamespace && Namespace.Length > 0)
            {
                RemoveIndent();
            }

            return string.Join("\n", result);
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
                ProjectConfigSseEndpoint config = ProjectManager.Config.sseEndpoint;
                if (config.host != null)
                {
                    options.Add("options.host = " + JsonConvert.SerializeObject(config.host) + ";");
                }
                if (config.port != null)
                {
                    options.Add("options.port = " + config.port + ";");
                }
                if (config.useHttps != null)
                {
                    options.Add("options.useHttps = " + JsonConvert.SerializeObject(config.useHttps) + ";");
                }
                if (config.withCredentials != null)
                    options.Add("options.withCredentials = " + JsonConvert.SerializeObject(config.withCredentials) + ";");

                if(options.Count > 0)
                {
                    AddTxtOpen("protected override configure(options: AventusSharp.SSE.ConnectionOptions): AventusSharp.SSE.ConnectionOptions {", result);
                    foreach(string option in options)
                    {
                        AddTxt(option, result);
                    }
                    AddTxt("return super.configure(options);", result);
                    AddTxtClose("}", result);
                }

                AddTxtOpen("protected override get path(): string {", result);
                AddTxt("return " + JsonConvert.SerializeObject(path) + ";", result);
                AddTxtClose("}", result);
            }
            return string.Join("\n", result);
        }

        protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return applyReplacer(ProjectManager.Config.replacer.sseEndPoint, fullname, result);
        }
    }

}
