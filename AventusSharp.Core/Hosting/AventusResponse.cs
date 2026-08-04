namespace AventusSharp.Hosting;

/// <summary>
/// Default in-memory response used by non-ASP.NET hosts.
/// </summary>
public sealed class AventusResponse : IAventusResponse, IDisposable
{
    public int StatusCode { get; set; } = 200;
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public Stream Body { get; set; } = new MemoryStream();
    public IDictionary<string, string[]> Headers { get; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        Body.Dispose();
    }
}
