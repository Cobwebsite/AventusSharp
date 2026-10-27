namespace AventusSharp.Maui.Routes;

using AventusSharp.Hosting;
using AventusSharp.Routes;
using AventusSharp.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.Maui;

public static class RouterAdapter
{
    private static bool Registered { get; set; } = false;
    private static List<Func<IAventusContext, Func<Task>, Task>> MiddlewareBefore { get; set; } = [];
    private static List<Func<IAventusContext, Func<Task>, Task>> MiddlewareAfter { get; set; } = [];

    [JSInvokable]
    public static Task<AdapterResponse> EmulateRequest(string method, string url, byte[]? body, string? contentType)
    {
        return EmulateRequest(method, url, body, contentType, null);
    }

    public static async Task<AdapterResponse> EmulateRequest(string method, string url, byte[]? body, string? contentType, IServiceProvider? customServices)
    {
        IServiceProvider services = customServices ?? IPlatformApplication.Current?.Services ?? throw new InvalidOperationException(AventusTranslations.Get(AventusMessageKeys.Routes.ServiceProviderUnavailable));

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

        await ExecuteMiddlewares(context, MiddlewareBefore, async () =>
        {
            RouterResolve? resolve = await RouterMiddleware.Resolve(context);

            if (resolve is not null)
            {
                await RouterMiddleware.OnRequest(context, resolve);
            }
            else if (context.Response.StatusCode < 400)
            {
                context.Response.StatusCode = 404;
            }

            await ExecuteMiddlewares(context, MiddlewareAfter, () => Task.CompletedTask);
        });


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

        Dictionary<string, string[]> headers = response.Headers.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        if (response.ContentType is not null && !headers.ContainsKey("Content-Type"))
        {
            headers["Content-Type"] = [response.ContentType];
        }

        return new AdapterResponse(response.StatusCode, content, headers);
    }

    private static Task ExecuteMiddlewares(IAventusContext context, List<Func<IAventusContext, Func<Task>, Task>> middlewares, Func<Task> finalAction)
    {
        Func<Task> next = finalAction;

        for (int i = middlewares.Count - 1; i >= 0; i--)
        {
            Func<IAventusContext, Func<Task>, Task> middleware = middlewares[i];
            Func<Task> currentNext = next;

            next = () => middleware(context, currentNext);
        }

        return next();
    }

    internal static void AddMiddleware(Func<IAventusContext, Func<Task>, Task> middleware)
    {
        if (Registered)
        {
            MiddlewareAfter.Add(middleware);
        }
        else
        {
            MiddlewareBefore.Add(middleware);
        }
    }

    internal static void SetRegistered()
    {
        Registered = true;
    }
}

public sealed record AdapterResponse(
    int Status,
    byte[] Content,
    IReadOnlyDictionary<string, string[]> Headers);
