using AventusSharp.Hosting;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.AspNetCore.Hosting;

public sealed class AspNetCoreAventusRequest : IAventusRequest
{
    private readonly HttpRequest request;

    public string Method
    {
        get => request.Method;
        set => request.Method = value;
    }
    public string Path
    {
        get => request.Path.Value ?? string.Empty;
        set => request.Path = value;
    }
    public string QueryString
    {
        get => request.QueryString.Value ?? string.Empty;
        set => request.QueryString = new QueryString(value);
    }
    public string? ContentType
    {
        get => request.ContentType;
        set => request.ContentType = value;
    }
    public long? ContentLength
    {
        get => request.ContentLength;
        set => request.ContentLength = value;
    }
    public Stream Body
    {
        get => request.Body;
        set => request.Body = value;
    }
    public IDictionary<string, string[]> Headers { get; }

    public AspNetCoreAventusRequest(HttpRequest request)
    {
        this.request = request;
        Headers = new AspNetCoreHeaderDictionary(request.Headers);
    }
}
