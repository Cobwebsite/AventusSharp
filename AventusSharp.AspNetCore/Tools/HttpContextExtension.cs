using AventusSharp.AspNetCore.Hosting;
using AventusSharp.AspNetCore.Routes;
using AventusSharp.Routes;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.Tools;

public static class HttpContextExtension
{
    public static AventusContext GetAventusContext(this HttpContext context)
    {
        if (context.Items.ContainsKey("_realContext") && context.Items["_realContext"] is AventusContext result)
        {
            return result;
        }

        AventusContext res = new AventusContext(context);
        context.Items["_realContext"] = res;
        return res;
    }

    public static Task<RouterResolve?> Resolve(this HttpContext context)
    {
        return RouterAdapter.Resolve(context);
    }
}