using AventusSharp.Hosting;
using System.Threading.Tasks;

namespace AventusSharp.Routes;

/// <summary>
/// Executes the existing Aventus router through a host-independent entry point.
/// </summary>
public sealed class AventusRequestDispatcher : IAventusRequestDispatcher
{
    public async Task DispatchAsync(IAventusContext context)
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
    }
}
