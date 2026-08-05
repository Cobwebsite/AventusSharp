using AventusSharp.Routes;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.AspNetCore.Routes;

public static class RouterAdapter
{
    public static Task<RouterResolve?> Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RouterMiddleware.Resolve(context.GetAventusContext(), new()
        {
            {typeof(HttpContext), context}
        });
    }

    public static async Task OnRequest(HttpContext context, Func<Task> next)
    {
        RouterResolve? resolve = await Resolve(context);
        if (resolve is null)
        {
            await next();
            return;
        }

        await RouterMiddleware.OnRequest(context.GetAventusContext(), resolve);
    }
}
