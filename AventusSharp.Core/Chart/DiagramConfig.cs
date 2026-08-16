namespace AventusSharp.Chart;

public class DiagramConfig
{
    public required string MainName;
    public required string OutputDirectory;
    public bool GenerateMain;
    public bool UseNamespaceForMain;

    public DiagramConfigInternal ToInternal()
    {
        return new DiagramConfigInternal
        {
            MainName = MainName,
            GenerateMain = GenerateMain,
            UseNamespaceForMain = UseNamespaceForMain
        };
    }
}

public class DiagramConfigInternal
{
    public required string MainName;
    public bool GenerateMain;
    public bool UseNamespaceForMain;
}
