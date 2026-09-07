using System.Globalization;
using System.Resources;

namespace AventusSharp.Localization;

/// <summary>A startup snapshot of translation settings, usable without a host or dependency injection.</summary>
public sealed class AventusLocalizer
{
    private static readonly ResourceManager BuiltIn = new(
        "AventusSharp.Localization.Messages", typeof(AventusLocalizer).Assembly);
    private readonly Dictionary<(string Culture, string Key), string> messages;
    private readonly ResourceManager[] resources;
    private readonly IAventusMessageProvider? provider;

    public CultureInfo DefaultCulture { get; }
    public IReadOnlyList<CultureInfo> SupportedCultures { get; }

    public AventusLocalizer(Action<AventusTranslationOptions>? configure = null)
    {
        var options = new AventusTranslationOptions();
        configure?.Invoke(options);
        DefaultCulture = CultureInfo.GetCultureInfo(options.DefaultCulture);
        SupportedCultures = Array.AsReadOnly(options.SupportedCultures
            .Select(CultureInfo.GetCultureInfo).Append(DefaultCulture).Distinct().ToArray());
        messages = new(options.Messages);
        resources = options.Resources.ToArray();
        provider = options.Provider;
    }

    /// <summary>Translates a key using this instance's default language.</summary>
    public string Get(string key, params object?[] arguments) => Get(key, DefaultCulture, arguments);

    /// <summary>Translates and formats a key. Unknown keys are returned unchanged.</summary>
    public string Get(string key, CultureInfo culture, params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(culture);
        var template = FindCustom(key, culture)
            ?? FindCustom(key, DefaultCulture)
            ?? FindCustom(key, CultureInfo.GetCultureInfo("en"))
            ?? BuiltIn.GetString(key, culture);
        return template is null ? key : string.Format(culture, template, arguments);
    }

    private string? FindCustom(string key, CultureInfo culture)
    {
        for (var current = culture; ; current = current.Parent)
        {
            if (messages.TryGetValue((current.Name, key), out var value)) return value;
            value = provider?.GetTemplate(key, current);
            if (value is not null) return value;
            // Exact resource sets let the parent-culture search retain override precedence.
            foreach (var resource in resources)
            {
                value = resource.GetResourceSet(current, true, false)?.GetString(key);
                if (value is not null) return value;
            }
            if (current.Equals(CultureInfo.InvariantCulture)) return null;
        }
    }
}
