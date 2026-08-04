namespace AventusSharp.Chart;

public class DiagramConfig
{
    public required string MainName;
    public required string OutputDirectory;
    public bool GenerateMain;

    public DiagramConfigInternal ToInternal()
    {
        return new DiagramConfigInternal
        {
            MainName = MainName,
            GenerateMain = GenerateMain
        };
    }
}

public class DiagramConfigInternal
{
    public required string MainName;
    public bool GenerateMain;
}
