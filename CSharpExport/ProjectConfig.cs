namespace CSharpExport;

internal sealed class ProjectConfig
{
    public required string CsProj { get; set; }
    public required string OutputPath { get; set; }
    public string AssemblyName { get; set; } = "CSharpExport.Generated";
    public List<ExportPackage> Packages { get; set; } = new();
    public bool ExportEnumByDefault { get; set; } = false;
    public bool ExportErrorsByDefault { get; set; } = true;

    public void Prepare(string configFile)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(configFile))!;
        CsProj = Path.GetFullPath(CsProj, directory);
        OutputPath = Path.GetFullPath(OutputPath, directory);
        if (!File.Exists(CsProj)) throw new FileNotFoundException("Projet source introuvable.", CsProj);
        if (Path.GetDirectoryName(CsProj) == OutputPath)
            throw new InvalidOperationException("La sortie doit être distincte du projet source.");
    }
}

internal sealed class ExportPackage
{
    public required string Id { get; set; }
    public required string Version { get; set; }
}
