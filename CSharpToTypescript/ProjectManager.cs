
using CSharpToTypescript.Container;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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
                INamespaceSymbol? rootNamespace = Compilation.GlobalNamespace.GetNamespaceMembers().First(p => p.Name == proj.Name);
                if (rootNamespace != null)
                {
                    CurrentAssemblyName = rootNamespace.Name;
                    LoadNamespace(rootNamespace, result);
                }
                FileToWrite.WriteAll();
            }
        }

        private bool Build()
        {
            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            string cmd = "build " + Config.csProj + " --no-dependencies -v m";
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
            List<string> splitted = output.Split("\n").ToList();
            if (splitted.Count < 4)
            {
                Console.WriteLine("The build result seems to be wrong. Please send the result below to an admin");
                Console.WriteLine(output);
                Console.WriteLine("splitted count : " + splitted.Count);
                return false;
            }
            string nbErrors = splitted[splitted.Count - 4];
            if (!nbErrors.Contains("0"))
            {
                Console.WriteLine(output);
                return false;
            }
            string outputPath = "";
            for (int i = splitted.Count - 1; i >= 0; i--)
            {
                if (splitted[i].Contains("->"))
                {
                    outputPath = splitted[i].Split("->")[1].Trim();
                }
            }
            if (outputPath == "")
            {
                Console.WriteLine(output);
                return false;
            }
            List<string> outputSplitted = outputPath.Split(Path.DirectorySeparatorChar).ToList();
            outputSplitted.RemoveAt(outputSplitted.Count - 1);
            Config.outputDir = string.Join(Path.DirectorySeparatorChar, outputSplitted);
            Config.compiledAssembly = Assembly.LoadFrom(outputPath);
            return true;
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
