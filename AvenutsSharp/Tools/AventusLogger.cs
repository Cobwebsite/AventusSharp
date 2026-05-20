using System;
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
            return new AventusConsoleLogger("AventusSharp");
            // using var factory = LoggerFactory.Create(builder => builder.AddConsole());
            // return factory.CreateLogger("AventusSharp");
        }
        catch
        {
            return NullLogger.Instance;
        }
    }
}

public class AventusConsoleLogger : ILogger
{
    private readonly string _categoryName;

    public AventusConsoleLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message = formatter(state, exception);

        // Choisir une couleur ou un préfixe selon le niveau de log
        (string? prefix, ConsoleColor color) = logLevel switch
        {
            LogLevel.Information => ("[INFO]", ConsoleColor.Cyan),
            LogLevel.Warning => ("[WARN]", ConsoleColor.Yellow),
            LogLevel.Error => ("[ERR ]", ConsoleColor.Red),
            LogLevel.Critical => ("[CRIT]", ConsoleColor.DarkRed),
            _ => ("[LOG ]", ConsoleColor.Gray)
        };

        // Écriture personnalisée dans la console
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"{prefix} [{_categoryName}] : " + message);
        Console.ForegroundColor = originalColor;

        if (exception != null)
        {
            Console.WriteLine(exception.ToString());
        }
    }
}