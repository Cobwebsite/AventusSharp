using AventusSharp.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AventusSharp.Maui;

/// <summary>
/// Executes virtual HTTP requests in-process for a MAUI WebView host.
/// </summary>
public sealed class AventusMauiBridge
{
    private readonly IAventusRequestDispatcher dispatcher;
    private readonly IServiceScopeFactory scopeFactory;

    public AventusMauiBridge(
        IAventusRequestDispatcher dispatcher,
        IServiceScopeFactory scopeFactory)
    {
        this.dispatcher = dispatcher;
        this.scopeFactory = scopeFactory;
    }

    public async Task<AventusBridgeResponse> ExecuteAsync(
        AventusBridgeRequest bridgeRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeRequest);

        using IServiceScope scope = scopeFactory.CreateScope();
        var uri = new Uri(bridgeRequest.Url, UriKind.Absolute);
        var request = new AventusRequestBase
        {
            Method = bridgeRequest.Method,
            Path = uri.AbsolutePath,
            QueryString = uri.Query,
            ContentType = bridgeRequest.ContentType,
            ContentLength = bridgeRequest.Body?.LongLength,
            Body = bridgeRequest.Body is { Length: > 0 }
                ? new MemoryStream(bridgeRequest.Body, writable: false)
                : Stream.Null
        };
        foreach (KeyValuePair<string, string[]> header in bridgeRequest.Headers)
        {
            request.Headers[header.Key] = header.Value;
        }

        using var response = new AventusResponseBase();
        var context = new AventusContextBase(
            request,
            response,
            scope.ServiceProvider,
            cancellationToken);

        await dispatcher.DispatchAsync(context);

        byte[] content;
        if (response.Body is MemoryStream memoryStream)
        {
            content = memoryStream.ToArray();
        }
        else
        {
            using var output = new MemoryStream();
            if (response.Body.CanSeek)
            {
                response.Body.Position = 0;
            }
            await response.Body.CopyToAsync(output, cancellationToken);
            content = output.ToArray();
        }

        var headers = response.Headers.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        if (response.ContentType is not null && !headers.ContainsKey("Content-Type"))
        {
            headers["Content-Type"] = [response.ContentType];
        }

        return new AventusBridgeResponse(response.StatusCode, content, headers);
    }
}

public sealed record AventusBridgeRequest(
    string Method,
    string Url,
    byte[]? Body = null,
    string? ContentType = null,
    IReadOnlyDictionary<string, string[]>? RequestHeaders = null)
{
    public IReadOnlyDictionary<string, string[]> Headers { get; } =
        RequestHeaders ?? new Dictionary<string, string[]>();
}

public sealed record AventusBridgeResponse(
    int Status,
    byte[] Content,
    IReadOnlyDictionary<string, string[]> Headers);
