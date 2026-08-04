using AventusSharp.Hosting;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.AspNetCore.Hosting;

public sealed class AspNetCoreAventusResponse : IAventusResponse
{
    private readonly HttpResponse response;

    public int StatusCode
    {
        get => response.StatusCode;
        set => response.StatusCode = value;
    }
    public string? ContentType
    {
        get => response.ContentType;
        set => response.ContentType = value;
    }
    public long? ContentLength
    {
        get => response.ContentLength;
        set => response.ContentLength = value;
    }
    public Stream Body
    {
        get => response.Body;
        set => response.Body = value;
    }
    public IDictionary<string, string[]> Headers { get; }

    public AspNetCoreAventusResponse(HttpResponse response)
    {
        this.response = response;
        Headers = new AspNetCoreHeaderDictionary(response.Headers);
    }
}
