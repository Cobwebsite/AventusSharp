using Microsoft.AspNetCore.Http;

namespace AventusSharp.AspNetCore.Hosting;

public static class AspNetCoreContextAccessor
{
    private static readonly AsyncLocal<HttpContext?> CurrentContext = new();

    public static HttpContext? Current
    {
        get => CurrentContext.Value;
        internal set => CurrentContext.Value = value;
    }
}
