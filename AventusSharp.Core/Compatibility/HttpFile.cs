using AventusSharp.Tools;

namespace AventusSharp.Routes.Request;

/// <summary>
/// Represents a file received by an Aventus host.
/// </summary>
public class HttpFile
{
    private readonly string? tempDirectory;
    internal FileStream? stream;
    public string FormName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string Type { get; set; }

    public HttpFile(string formName, string filename, string filepath, string type)
    {
        FormName = formName;
        FileName = filename;
        FilePath = filepath;
        Type = type;
    }

    internal HttpFile(
        string formName,
        string filename,
        string filepath,
        string type,
        FileStream stream,
        string tempDirectory) : this(formName, filename, filepath, type)
    {
        this.stream = stream;
        this.tempDirectory = tempDirectory;
    }

    public bool IsInsideTemp =>
        tempDirectory is not null && FilePath.StartsWith(tempDirectory);

    public bool Move(string path)
    {
        ResultWithRouteError<bool> result = MoveWithError(path);
        return result.Success && result.Result;
    }

    public ResultWithRouteError<bool> MoveWithError(string path)
    {
        var result = new ResultWithRouteError<bool>();
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }
            File.Move(FilePath, path, true);
            FilePath = path;
            result.Result = true;
        }
        catch (Exception exception)
        {
            result.Errors.Add(new RouteError(RouteErrorCode.CantMoveFile, exception));
        }
        return result;
    }

    public bool Copy(string path)
    {
        ResultWithRouteError<bool> result = CopyWithError(path);
        return result.Success && result.Result;
    }

    public ResultWithRouteError<bool> CopyWithError(string path)
    {
        var result = new ResultWithRouteError<bool>();
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }
            File.Copy(FilePath, path, true);
            FilePath = path;
            result.Result = true;
        }
        catch (Exception exception)
        {
            result.Errors.Add(new RouteError(RouteErrorCode.CantMoveFile, exception));
        }
        return result;
    }
}
