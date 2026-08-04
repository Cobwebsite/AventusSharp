using System.Security.Claims;
using AventusSharp.Hosting;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.AspNetCore.Hosting;

public sealed class AspNetCoreAventusContext : IAventusContext
{
    private readonly HttpContext context;

    public HttpContext NativeContext => context;
    public IAventusRequest Request { get; }
    public IAventusResponse Response { get; }
    public IServiceProvider Services => context.RequestServices;
    public ClaimsPrincipal User
    {
        get => context.User;
        set => context.User = value;
    }
    public IDictionary<object, object?> Items => context.Items;
    public CancellationToken CancellationToken => context.RequestAborted;

    public AspNetCoreAventusContext(HttpContext context)
    {
        this.context = context;
        Request = new AspNetCoreAventusRequest(context.Request);
        Response = new AspNetCoreAventusResponse(context.Response);
    }
}
