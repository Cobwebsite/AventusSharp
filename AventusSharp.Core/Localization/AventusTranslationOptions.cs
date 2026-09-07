using System.Globalization;
using System.Resources;
using System.Text;

namespace AventusSharp.Localization;

/// <summary>Configures library translations and application messages at startup.</summary>
public sealed class AventusTranslationOptions
{
    /// <summary>The language used outside a culture scope, and the fallback for application messages.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Languages accepted by the ASP.NET Core request localization middleware.</summary>
    public List<string> SupportedCultures { get; } = ["en", "fr"];

    /// <summary>Optional custom provider, consulted before resource files.</summary>
    public IAventusMessageProvider? Provider { get; set; }

    internal Dictionary<(string Culture, string Key), string> Messages { get; } = new();
    internal List<ResourceManager> Resources { get; } = new();

    /// <summary>Adds or replaces a composite-format template, e.g. "Hello {0}".</summary>
    public AventusTranslationOptions Add(string culture, string key, string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(template);
        CompositeFormat.Parse(template);
        var name = CultureInfo.GetCultureInfo(culture).Name;
        Messages[(name, key)] = template;
        if (!SupportedCultures.Contains(name, StringComparer.OrdinalIgnoreCase))
            SupportedCultures.Add(name);
        return this;
    }

    /// <summary>Adds application .resx resources. Earlier registrations take precedence.</summary>
    public AventusTranslationOptions AddResources(ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Resources.Add(resources);
        return this;
    }
}
