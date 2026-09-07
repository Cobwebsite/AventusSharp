using System.Globalization;

namespace AventusSharp.Localization;

/// <summary>Supplies message templates. Return null to use the next fallback.</summary>
public interface IAventusMessageProvider
{
    string? GetTemplate(string key, CultureInfo culture);
}
