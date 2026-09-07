using System.Globalization;
using System.Threading;

namespace AventusSharp.Localization;

/// <summary>Shared translation entry point for AventusSharp and application code.</summary>
public static class AventusTranslations
{
    private sealed record Context(AventusLocalizer Localizer, CultureInfo Culture);
    private static readonly AsyncLocal<Context?> Current = new();
    private static AventusLocalizer defaultLocalizer = new();

    /// <summary>The process-wide startup configuration. Hosts can use isolated scopes instead.</summary>
    public static AventusLocalizer Default => Volatile.Read(ref defaultLocalizer);

    /// <summary>Configures translations for console apps, workers and desktop applications.</summary>
    public static void Configure(Action<AventusTranslationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Volatile.Write(ref defaultLocalizer, new AventusLocalizer(configure));
    }

    public static string Get(string key, params object?[] arguments)
    {
        var context = Current.Value;
        var localizer = context?.Localizer ?? Default;
        return localizer.Get(key, context?.Culture ?? localizer.DefaultCulture, arguments);
    }

    /// <summary>Changes the translation language for this async flow until disposed.</summary>
    public static IDisposable UseCulture(string culture) =>
        Use(Current.Value?.Localizer ?? Default, CultureInfo.GetCultureInfo(culture));

    /// <summary>Uses an isolated configuration and language for this async flow.</summary>
    public static IDisposable Use(AventusLocalizer localizer, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        var previous = Current.Value;
        Current.Value = new(localizer, culture ?? localizer.DefaultCulture);
        return new Scope(previous);
    }

    private sealed class Scope(Context? previous) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            Current.Value = previous;
            disposed = true;
        }
    }
}
