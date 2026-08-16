using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AventusSharp.Tools;

public static class AventusLogger
{
    public static ILogger Instance { get; set; } = NullLogger.Instance;

    public static void Initialize(ILoggerFactory? factory, ILoggerFactory? defaultLogger = null)
    {
        if (factory is not null)
        {
            Instance = factory.CreateLogger("AventusSharp");
        }
        else if (defaultLogger is not null)
        {
            Instance = defaultLogger.CreateLogger("AventusSharp");
        }
    }

}
