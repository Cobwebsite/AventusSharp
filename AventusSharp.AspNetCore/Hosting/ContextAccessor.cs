using Microsoft.AspNetCore.Http;

namespace AventusSharp.AspNetCore.Hosting;

public static class ContextAccessor
{
    private static readonly AsyncLocal<HttpContext?> CurrentContext = new();

    public static HttpContext? Current
    {
        get => CurrentContext.Value;
        internal set => CurrentContext.Value = value;
    }
}
