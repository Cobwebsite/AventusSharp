using AventusSharp.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.Routes;

public static class AspNetCoreRouterAdapter
{
    public static async Task OnRequest(HttpContext context, Func<Task> next)
    {
        var aventusContext = new AspNetCoreAventusContext(context);
        RouterResolve? resolve = await RouterMiddleware.Resolve(aventusContext);
        if (resolve is null)
        {
            await next();
            return;
        }

        await RouterMiddleware.OnRequest(aventusContext, resolve);
    }
}
