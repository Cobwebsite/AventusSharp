using System.Security.Claims;

namespace AventusSharp.Hosting;

/// <summary>
/// Describes the host-independent state available while an Aventus request is executed.
/// </summary>
public interface IAventusContext
{
    IAventusRequest Request { get; }
    IAventusResponse Response { get; }
    IServiceProvider Services { get; }
    ClaimsPrincipal User { get; set; }
    IDictionary<object, object?> Items { get; }
    CancellationToken CancellationToken { get; }
}
