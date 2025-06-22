
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.Mcp;

public static class McpSessionContext
{
    public const string McpSessionIdHeaderName = "Mcp-Session-Id";
    private static ConcurrentDictionary<string, HttpContext> _cache = new();

    public static async Task SetSession(string sessionId, HttpContext context)
    {
        
    }
    public static HttpContext? GetCurrentSession(string sessionId)
        => _cache.Where(kv => kv.Key.Equals(sessionId, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Value).FirstOrDefault();
}