namespace AventusSharp.Hosting;

/// <summary>
/// Describes the request data consumed by the Aventus router.
/// </summary>
public interface IAventusRequest
{
    string Method { get; set; }
    string Path { get; set; }
    string QueryString { get; set; }
    string? ContentType { get; set; }
    long? ContentLength { get; set; }
    Stream Body { get; set; }
    IDictionary<string, string[]> Headers { get; }
}
