using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AventusSharp.Tools;

internal static class AventusLogger
{
    private static ILogger? _logger;
    public static ILogger Instance => _logger ??= CreateDefaultLogger();
    public static void Initialize(IApplicationBuilder app)
    {
        var factory = app.ApplicationServices.GetService<ILoggerFactory>();
        if (factory != null)
        {
            _logger = factory.CreateLogger("AventusSharp");
        }
    }

    private static ILogger CreateDefaultLogger()
    {
        try
        {
            using var factory = LoggerFactory.Create(builder => builder.AddConsole());
            return factory.CreateLogger("AventusSharp");
        }
        catch
        {
            return NullLogger.Instance;
        }
    }
}