using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AventusSharp.Tools;

internal static class AventusLogger
{
    public static ILogger Instance { get; set; } = NullLogger.Instance;
}
