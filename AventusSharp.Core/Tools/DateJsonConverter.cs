using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SharpDate = AventusSharp.Data.Date;
using SharpDatetime = AventusSharp.Data.Datetime;

namespace AventusSharp.Tools;

internal static class DateJsonFormat
{
    internal static string WriteDate(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string WriteDatetime(DateTime value) =>
        (value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value).ToString("O", CultureInfo.InvariantCulture);

    internal static DateTime ReadDate(string text)
    {
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw new FormatException("Expected an ISO date (YYYY-MM-DD).");
        return value;
    }

    internal static DateTime ReadDatetime(string text)
    {
        // .NET DateTime has seven fractional digits (100 ns). Reject excess precision instead of silently losing it.
        if (!Regex.IsMatch(text, @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})?\z", RegexOptions.CultureInvariant))
            throw new FormatException("Expected an ISO date and time with at most seven fractional digits.");
        if (text.EndsWith("Z", StringComparison.Ordinal) || Regex.IsMatch(text, @"[+-]\d{2}:\d{2}\z"))
            return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None).UtcDateTime;
        return DateTime.SpecifyKind(DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None), DateTimeKind.Unspecified);
    }
}

/// <summary>Serializes an Aventus date as an ISO string, including when nested in an Aventus JSON object.</summary>
public sealed class DateJsonConverter : JsonConverter<SharpDate>
{
    public override void WriteJson(JsonWriter writer, SharpDate? value, JsonSerializer serializer)
    {
        if (value is null) writer.WriteNull();
        else writer.WriteValue(DateJsonFormat.WriteDate(value.DateTime));
    }

    public override SharpDate? ReadJson(JsonReader reader, Type objectType, SharpDate? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        try
        {
            if (reader.TokenType == JsonToken.String) return new SharpDate(DateJsonFormat.ReadDate((string)reader.Value!));
        }
        catch (FormatException error) { throw new JsonSerializationException("Invalid Aventus Date.", error); }
        throw new JsonSerializationException("Expected an ISO date string or null.");
    }
}

/// <summary>Serializes an Aventus datetime as ISO, preserving unspecified times and normalizing instants to UTC.</summary>
public sealed class DatetimeJsonConverter : JsonConverter<SharpDatetime>
{
    public override void WriteJson(JsonWriter writer, SharpDatetime? value, JsonSerializer serializer)
    {
        if (value is null) writer.WriteNull();
        else writer.WriteValue(DateJsonFormat.WriteDatetime(value.DateTime));
    }

    public override SharpDatetime? ReadJson(JsonReader reader, Type objectType, SharpDatetime? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        try
        {
            if (reader.TokenType == JsonToken.String) return new SharpDatetime(DateJsonFormat.ReadDatetime((string)reader.Value!));
            // Json.NET can eagerly parse ISO timestamps before invoking the converter.
            if (reader.TokenType == JsonToken.Date && reader.Value is DateTime value)
                return new SharpDatetime(value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value);
            if (reader.TokenType == JsonToken.Date && reader.Value is DateTimeOffset offset)
                return new SharpDatetime(offset.UtcDateTime);
        }
        catch (FormatException error) { throw new JsonSerializationException("Invalid Aventus Datetime.", error); }
        throw new JsonSerializationException("Expected an ISO datetime string or null.");
    }
}

/// <summary>System.Text.Json support for Aventus dates without application-level configuration.</summary>
public sealed class DateSystemJsonConverter : System.Text.Json.Serialization.JsonConverter<SharpDate>
{
    public override void Write(System.Text.Json.Utf8JsonWriter writer, SharpDate value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(DateJsonFormat.WriteDate(value.DateTime));

    public override SharpDate Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.String) throw new System.Text.Json.JsonException("Expected an ISO date string.");
        try { return new SharpDate(DateJsonFormat.ReadDate(reader.GetString()!)); }
        catch (FormatException error) { throw new System.Text.Json.JsonException("Invalid Aventus Date.", error); }
    }
}

/// <summary>System.Text.Json support for Aventus datetimes without application-level configuration.</summary>
public sealed class DatetimeSystemJsonConverter : System.Text.Json.Serialization.JsonConverter<SharpDatetime>
{
    public override void Write(System.Text.Json.Utf8JsonWriter writer, SharpDatetime value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(DateJsonFormat.WriteDatetime(value.DateTime));

    public override SharpDatetime Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.String) throw new System.Text.Json.JsonException("Expected an ISO datetime string.");
        try { return new SharpDatetime(DateJsonFormat.ReadDatetime(reader.GetString()!)); }
        catch (FormatException error) { throw new System.Text.Json.JsonException("Invalid Aventus Datetime.", error); }
    }
}
