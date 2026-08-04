namespace AventusSharp.Data;

public static class DataRuntime
{
    public static bool IsExportCommand =>
        Environment.GetCommandLineArgs().Contains("--export-info");
}
