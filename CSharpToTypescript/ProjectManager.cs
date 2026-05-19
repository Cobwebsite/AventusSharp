
using AventusSharp.Routes;
using AventusSharp.WebSocket;
using CSharpToTypescript.Container;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Project = Microsoft.CodeAnalysis.Project;

namespace CSharpToTypescript
{
    internal class ProjectManager
    {
        public static string? CurrentAssemblyName { get; private set; }
#pragma warning disable CS8618 // Un champ non-nullable doit contenir une valeur non-null lors de la fermeture du constructeur. Envisagez de déclarer le champ comme nullable.
        public static Compilation Compilation { get; private set; }
        public static ProjectConfig Config { get; private set; }
#pragma warning restore CS8618 // Un champ non-nullable doit contenir une valeur non-null lors de la fermeture du constructeur. Envisagez de déclarer le champ comme nullable.

        public static bool CompilingAventusSharp
        {
            get
            {
                return Config.compiledAssembly?.FullName?.StartsWith("AventusSharp,") == true;
            }
        }

        public Dictionary<string, List<BaseContainer>> files = new Dictionary<string, List<BaseContainer>>();

        public ProjectManager()
        {

        }

        public async Task Init(ProjectConfig config)
        {
            Config = config;
            if (!Build())
            {
                Console.WriteLine("Error : Compilation failed");
                return;
            }
            if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();
            using (var w = MSBuildWorkspace.Create())
            {
                Project proj = await w.OpenProjectAsync(config.csProj);
                Compilation = await proj.GetCompilationAsync() ?? throw new Exception("Can't compile");

                List<INamedTypeSymbol> result = new();
                string rootNamespaceName = proj.DefaultNamespace ?? proj.Name;
                INamespaceSymbol? rootNamespace = Compilation.GlobalNamespace.GetNamespaceMembers().First(p => p.Name == rootNamespaceName);
                if (rootNamespace != null)
                {
                    CurrentAssemblyName = proj.AssemblyName;
                    LoadNamespace(rootNamespace, result);
                }
                FileToWrite.WriteAll();

            }
                // Directory.Delete(Config.outputDir, true);
        }

        private bool Build()
        {
            string tempOutputDir = Path.Combine(Path.GetTempPath(), "AventusBuild_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempOutputDir);

            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;

            string cmd = $"build \"{Config.csProj}\" -v m -o \"{tempOutputDir}\" -nologo";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = "/C dotnet " + cmd;
            }
            else
            {
                p.StartInfo.FileName = "dotnet";
                p.StartInfo.Arguments = cmd;
            }
            p.Start();
            // Read the output stream first and then wait.
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                Console.WriteLine(output);
                return false;
            }
            string assemblyName = Path.GetFileNameWithoutExtension(Config.csProj) + ".dll";
            string outputPath = Path.Combine(tempOutputDir, assemblyName);

            if (!File.Exists(outputPath))
            {
                Console.WriteLine(output);
                return false;
            }

            if (Config.httpRouter.useCompiledDll || Config.wsEndpoint.useCompiledDll)
            {
                LoadHttpRoute(outputPath);
            }
            Config.outputDir = tempOutputDir;
            Config.compiledAssembly = Assembly.LoadFrom(outputPath);
            return true;
        }

        private void LoadHttpRoute(string dll)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;

            string cmd = $"\"{dll}\" --export-info";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = "/C dotnet " + cmd;
            }
            else
            {
                p.StartInfo.FileName = "dotnet";
                p.StartInfo.Arguments = cmd;
            }

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                Console.WriteLine(p.StandardError.ReadToEnd());
                Console.WriteLine(output);
                return;
            }

            string patternHttp = @"(?<=--- Routes HTTP ---\s+)(.*?)(?=\s+-------------------)";

            Match matchHttp = Regex.Match(output, patternHttp, RegexOptions.Singleline);

            if (matchHttp.Success)
            {
                string jsonClean = matchHttp.Value.Trim();
                try
                {
                    List<RouteExposeHttp>? result = JsonConvert.DeserializeObject<List<RouteExposeHttp>>(jsonClean);
                    if (result != null)
                    {
                        Dictionary<string, List<RouteExposeHttp>> routesHttp = new Dictionary<string, List<RouteExposeHttp>>();
                        foreach (RouteExposeHttp routeExpose in result)
                        {
                            if (!routesHttp.ContainsKey(routeExpose.ClassName))
                            {
                                routesHttp[routeExpose.ClassName] = new();
                            }
                            routesHttp[routeExpose.ClassName].Add(routeExpose);
                        }
                        Config.routesHttp = routesHttp;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Parsing error for http route : {ex.Message}");
                }
            }
        
            string patternWs = @"(?<=--- Routes HTTP ---\s+)(.*?)(?=\s+-------------------)";

            Match matchWs = Regex.Match(output, patternWs, RegexOptions.Singleline);

            if (matchWs.Success)
            {
                string jsonClean = matchWs.Value.Trim();
                try
                {
                    List<WsExpose>? result = JsonConvert.DeserializeObject<List<WsExpose>>(jsonClean);
                    if (result != null)
                    {
                        Dictionary<string, List<WsExpose>> routesWs = new Dictionary<string, List<WsExpose>>();
                        foreach (WsExpose routeExpose in result)
                        {
                            if (!routesWs.ContainsKey(routeExpose.ClassName))
                            {
                                routesWs[routeExpose.ClassName] = new();
                            }
                            routesWs[routeExpose.ClassName].Add(routeExpose);
                        }
                        Config.routesWs = routesWs;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Parsing error for ws route : {ex.Message}");
                }
            }
        
        }

        private void LoadNamespace(INamespaceSymbol @namespace, List<INamedTypeSymbol> result)
        {
            List<INamedTypeSymbol> resultTemp = @namespace.GetTypeMembers().ToList();
            foreach (INamedTypeSymbol type in resultTemp)
            {
                result.Add(type);
                FileToWrite.RegisterType(type);
            }

            var subNamesapce = @namespace.GetNamespaceMembers();

            foreach (INamespaceSymbol symbol in subNamesapce)
            {
                LoadNamespace(symbol, result);
            }
        }

    }
}
