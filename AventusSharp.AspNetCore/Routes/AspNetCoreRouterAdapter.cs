using AventusSharp.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.Routes;

public static class AspNetCoreRouterAdapter
{
    public static Task<RouterResolve?> Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RouterMiddleware.Resolve(new AspNetCoreAventusContext(context));
    }

    public static async Task OnRequest(HttpContext context, Func<Task> next)
    {
        RouterResolve? resolve = await Resolve(context);
        if (resolve is null)
        {
            await next();
            return;
        }

        var aventusContext = new AspNetCoreAventusContext(context);
        await RouterMiddleware.OnRequest(aventusContext, resolve);
    }
}
