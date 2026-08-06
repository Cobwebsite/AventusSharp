namespace AventusSharp.Maui.Routes;

using AventusSharp.Hosting;
using AventusSharp.Routes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.Maui;

public static class RouterAdapter
{

    [JSInvokable]
    public static Task<AdapterResponse> EmulateRequest(string method, string url, byte[]? body, string? contentType)
    {
        return EmulateRequest(method, url, body, contentType, null);
    }

    public static async Task<AdapterResponse> EmulateRequest(string method, string url, byte[]? body, string? contentType, IServiceProvider? customServices)
    {
        IServiceProvider services = customServices ?? IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Can't load the Service provider");

        IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        using IServiceScope scope = scopeFactory.CreateScope();

        var uri = new Uri(url, UriKind.Absolute);
        var request = new AventusRequestBase
        {
            Method = method,
            Path = uri.AbsolutePath,
            QueryString = uri.Query,
            ContentType = contentType,
            ContentLength = body?.LongLength,
            Body = body is { Length: > 0 }
                ? new MemoryStream(body, writable: false)
                : Stream.Null
        };
        // foreach (KeyValuePair<string, string[]> header in headers)
        // {
        //     request.Headers[header.Key] = header.Value;
        // }

        using var response = new AventusResponseBase();
        var context = new AventusContextBase(request, response, scope.ServiceProvider);

        RouterResolve? resolve = await RouterMiddleware.Resolve(context);
        if (resolve is not null)
        {
            await RouterMiddleware.OnRequest(context, resolve);
        }
        else if (context.Response.StatusCode < 400)
        {
            context.Response.StatusCode = 404;
        }


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
            await response.Body.CopyToAsync(output);
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

        return new AdapterResponse(response.StatusCode, content, headers);
    }

}

public sealed record AdapterResponse(
    int Status,
    byte[] Content,
    IReadOnlyDictionary<string, string[]> Headers);