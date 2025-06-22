using Microsoft.Extensions.DependencyInjection;

namespace AventusSharp.Mcp;

public static class AventusMcpExtension
{
    public static IServiceCollection AddAventusMcp(this IServiceCollection services)
    {
        var b = services.AddMcpServer();
        b.WithHttpTransport(options =>
        {
            options.ConfigureSessionOptions = async (httpContext, mcpServerOptions, cancellationToken) =>
            {
                var sessionId = httpContext.Response.Headers[McpSessionContext.McpSessionIdHeaderName];
                if (string.IsNullOrWhiteSpace(sessionId))
                    return;

                mcpServerOptions.KnownClientInfo = new ImplementationWithContext()
                {
                    Name = mcpServerOptions.KnownClientInfo?.Name ?? "Unknown",
                    Version = mcpServerOptions.KnownClientInfo?.Version ?? "1.0.0",
                    SessionId = sessionId.ToString()
                };
                await McpSessionContext.SetSession(sessionId!, httpContext);
            };
        });
        return services;
    }
}