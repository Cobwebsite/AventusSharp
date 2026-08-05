namespace AventusSharp.Hosting;

/// <summary>
/// Default in-memory request used by non-ASP.NET hosts.
/// </summary>
public sealed class AventusRequestBase : IAventusRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string QueryString { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public Stream Body { get; set; } = Stream.Null;
    public IDictionary<string, string[]> Headers { get; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
}
