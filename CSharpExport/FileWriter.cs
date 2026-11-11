namespace CSharpExport;

internal sealed class FileWriter
{
    private readonly List<string> content = new();
    private int indent;

    public string GetIndentedText(string text) => new string('\t', indent) + text;
    public void AddTxt(string text, List<string>? result = null) =>
        (result ?? content).Add(GetIndentedText(text));
    public void AddTxt(IEnumerable<string> texts, List<string>? result = null)
    {
        foreach (var text in texts) AddTxt(text, result);
    }
    public void AddTxtOpen(string text, List<string>? result = null)
    {
        AddTxt(text, result);
        AddIndent();
    }
    public void AddTxtClose(string text, List<string>? result = null)
    {
        RemoveIndent();
        AddTxt(text, result);
    }
    public void AddIndent() => indent++;
    public void RemoveIndent()
    {
        if (indent == 0) throw new InvalidOperationException("Indentation négative.");
        indent--;
    }
    public string GetContent() => string.Join(Environment.NewLine, content) + Environment.NewLine;
    public static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
