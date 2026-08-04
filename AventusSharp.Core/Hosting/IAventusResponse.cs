namespace AventusSharp.Hosting;

/// <summary>
/// Describes the response destination used by an Aventus response.
/// </summary>
public interface IAventusResponse
{
    int StatusCode { get; set; }
    string? ContentType { get; set; }
    long? ContentLength { get; set; }
    Stream Body { get; set; }
    IDictionary<string, string[]> Headers { get; }
}
