using System.Xml.Linq;

namespace CSharpExport;

internal sealed class ProjectConfig
{
    public required string CsProj { get; set; }
    public required string OutputPath { get; set; }
    public string AssemblyName { get; set; } = "CSharpExport.Generated";
    public string? TargetFramework { get; set; }
    public List<ExportPackage> Packages { get; set; } = new();
    public bool ExportEnumByDefault { get; set; } = false;
    public bool ExportErrorsByDefault { get; set; } = true;
    public bool ExportHttpRouteByDefault { get; set; } = true;
    public HttpRouterConfig HttpRouter { get; set; } = new();

    public void Prepare(string configFile)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(configFile))!;
        CsProj = Path.GetFullPath(CsProj, directory);
        OutputPath = Path.GetFullPath(OutputPath, directory);
        if (HttpRouter.ParentFile != null)
            HttpRouter.ParentFile = Path.GetFullPath(HttpRouter.ParentFile, directory);
        if (!File.Exists(CsProj)) throw new FileNotFoundException("Projet source introuvable.", CsProj);
        var projectXml = XDocument.Load(CsProj);
        TargetFramework ??= projectXml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TargetFramework")?.Value.Trim();
        TargetFramework ??= projectXml.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "TargetFrameworks")?.Value.Split(';')[0].Trim();
        if (string.IsNullOrWhiteSpace(TargetFramework))
            throw new InvalidOperationException("Définir targetFramework dans export.json pour ce projet source.");
        if (Path.GetDirectoryName(CsProj) == OutputPath)
            throw new InvalidOperationException("La sortie doit être distincte du projet source.");
    }
}

internal sealed class ExportPackage
{
    public required string Id { get; set; }
    public required string Version { get; set; }
}

internal sealed class HttpRouterConfig
{
    public bool CreateRouter { get; set; }
    public string RouterName { get; set; } = "GeneratedRouter";
    public string Uri { get; set; } = "";
    public string? Host { get; set; }
    public string Parent { get; set; } = "HttpRouteClient";
    public string? ParentFile { get; set; }
    public string? Namespace { get; set; } = "Routes";
}

internal sealed class CompiledHttpRoute
{
    public required string ClassName { get; set; }
    public required string MethodName { get; set; }
    public required string BaseUrl { get; set; }
    public required string Pattern { get; set; }
    public int Method { get; set; }
    public List<string> Params { get; set; } = new();
}
