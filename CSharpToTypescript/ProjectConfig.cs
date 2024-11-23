using AventusSharp.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CSharpToTypescript
{
    internal class ProjectConfig
    {
        public string csProj = @"D:\Aventus\AvenutsSharp\AvenutsSharp\AventusSharp.csproj";
        public string outputPath = @"D:\Aventus\AvenutsSharp\AvenutsSharp\AventusJs\src";
        public bool exportEnumByDefault = false;
        public bool exportStorableByDefault = true;
        public bool exportHttpRouteByDefault = true;
        public bool exportWsEndPointByDefault = true;
        public bool exportWsEventByDefault = true;
        public bool exportWsRouteByDefault = true;
        public bool exportErrorsByDefault = true;


        private string? _basedir;
        public Assembly? compiledAssembly;
        public string outputDir = "";
        public string baseDir
        {
            get
            {
                _basedir ??= Path.GetDirectoryName(csProj) ?? "";
                return _basedir;
            }
        }
        public bool useNamespace = true;
        private string configPathDir = "";

        public ProjectConfigReplacer replacer = new ProjectConfigReplacer();
        public ProjectConfigHttpRouter httpRouter = new ProjectConfigHttpRouter();
        public ProjectConfigWsEndpoint wsEndpoint = new ProjectConfigWsEndpoint();

        public ProjectConfig()
        {
        }


        public void Prepare(string configPath)
        {
            string? configPathDir = Path.GetDirectoryName(configPath);
            if (configPathDir != null)
            {
                this.configPathDir = configPathDir;
                csProj = AbsoluteUrl(csProj);
                outputPath = AbsoluteUrl(outputPath);
            }
        }

        public string AbsoluteUrl(string url)
        {
            if (url.StartsWith("."))
            {
                return Path.GetFullPath(Path.Combine(configPathDir, url));
            }
            return url;
        }
        
    }

    #region http

    public class ProjectConfigHttpRouter
    {
        public bool createRouter = false;
        public string routerName = "GeneratedRouter";
        public string uri = "";
        public string? host;
        public string parent = "Aventus.HttpRouter";
        public string? parentFile;
        [JsonProperty("namespace")]
        public string? _namespace = "Routes";
    }

    public class ProjectConfigWsEndpoint
    {
        public string prefix = "";
        public string? host;
        public int? port;
        public bool? useHttps;
        public string parent = "AventusSharp.WebSocket.EndPoint";
        public string? parentFile;
    }
    #endregion


    #region replacer

    public class ProjectConfigReplacer
    {
        public ProjectConfigReplacerPart all = new ProjectConfigReplacerPart();
        public ProjectConfigReplacerPart genericError = new ProjectConfigReplacerPart();
        public ProjectConfigReplacerPart httpRouter = new ProjectConfigReplacerPart();
        public ProjectConfigReplacerPart normalClass = new ProjectConfigReplacerPart();
        public ProjectConfigReplacerPart storable = new ProjectConfigReplacerPart();
        public ProjectConfigReplacerPart withError = new ProjectConfigReplacerPart();
        public ProjectConfigReplacerPart wsEndPoint = new ProjectConfigReplacerPart();
        public ProjectConfigReplacerPart wsEvent = new ProjectConfigReplacerPart();
        public ProjectConfigReplacerPart wsRouter = new ProjectConfigReplacerPart();

    }

    public class ProjectConfigReplacerPart
    {
        public Dictionary<string, ProjectConfigReplacerInfo> type = new Dictionary<string, ProjectConfigReplacerInfo>();
        public Dictionary<string, ProjectConfigReplacerInfo> result = new Dictionary<string, ProjectConfigReplacerInfo>();
    }

    public class ProjectConfigReplacerInfo
    {
        public string result = "";
        public string file = "";
        public bool useTypeImport = false;
    }

    #endregion
}
