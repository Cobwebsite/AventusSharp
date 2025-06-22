using ModelContextProtocol.Protocol;

namespace AventusSharp.Mcp;

public sealed class ImplementationWithContext : Implementation
{
	public string SessionId { get; init; } = string.Empty;
}