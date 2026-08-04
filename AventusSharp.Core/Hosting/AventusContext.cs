using System.Security.Claims;

namespace AventusSharp.Hosting;

/// <summary>
/// Default host-independent Aventus context.
/// </summary>
public sealed class AventusContext : IAventusContext
{
    public IAventusRequest Request { get; }
    public IAventusResponse Response { get; }
    public IServiceProvider Services { get; }
    public ClaimsPrincipal User { get; set; } = new();
    public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public CancellationToken CancellationToken { get; }

    public AventusContext(
        IAventusRequest request,
        IAventusResponse response,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        Request = request;
        Response = response;
        Services = services;
        CancellationToken = cancellationToken;
    }
}
