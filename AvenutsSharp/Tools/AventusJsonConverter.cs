using AventusSharp.Data.CustomTableMembers;
using AventusSharp.Tools.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AventusSharp.Tools;

/// <summary>
/// Custom converter to add type when we need it (avoid dico and list bc crash in js)
/// </summary>
public class AventusJsonConverter : JsonConverter<object?>
{
    private readonly List<string> propToRemove = new() { };

    /// <summary>
    /// always true because we can always convert until object
    /// </summary>
    /// <param name="objectType"></param>
    /// <returns></returns>
    public override bool CanConvert(Type objectType)
    {
        return true;
    }

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        JsonSerializerOptions clonedOptions = new JsonSerializerOptions(options);
        clonedOptions.Converters.Remove(clonedOptions.Converters
            .First(c => c.GetType() == typeof(AventusJsonConverter)));

        return JsonSerializer.Deserialize(ref reader, typeToConvert, clonedOptions);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            return;
        }
        lock (value)
        {
            Type type = value.GetType();

            if (type.IsPrimitive || Type.GetTypeCode(type) != TypeCode.Object)
            {
                JsonSerializerOptions clonedOptions = new JsonSerializerOptions(options);
                clonedOptions.Converters.Remove(clonedOptions.Converters
                    .First(c => c.GetType() == typeof(AventusJsonConverter)));
                JsonSerializer.Serialize(writer, value, clonedOptions);
            }
            else if (type.IsEnum)
            {
                JsonSerializer.Serialize(writer, value.ToString());
            }
            else if (typeof(IDictionary).IsAssignableFrom(type))
            {
                IDictionary dict = (IDictionary)value;
                writer.WriteStartObject();
                writer.WriteString("$type", "Aventus.Map");
                writer.WritePropertyName("values");
                writer.WriteStartArray();
                foreach (DictionaryEntry entry in dict)
                {
                    writer.WriteStartArray();
                    JsonSerializer.Serialize(writer, entry.Key, options);
                    JsonSerializer.Serialize(writer, entry.Value, options);
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                writer.WriteStartArray();
                foreach (object? item in (IEnumerable)value)
                {
                    JsonSerializer.Serialize(writer, item, options);
                }
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("$type", type.FullName?.Split('`')[0] + ", " + type.Assembly.GetName().Name);

                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.GetCustomAttribute<NoExport>() != null || !prop.CanRead || prop.GetIndexParameters().Length > 0)
                        continue;

                    object? propValue = prop.GetValue(value);
                    if (propValue != null && !propToRemove.Contains(prop.Name))
                    {
                        writer.WritePropertyName(prop.Name);
                        JsonSerializer.Serialize(writer, propValue, options);
                    }
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.GetCustomAttribute<NoExport>() != null)
                        continue;

                    object? fieldValue = field.GetValue(value);
                    if (fieldValue != null && !propToRemove.Contains(field.Name))
                    {
                        writer.WritePropertyName(field.Name);
                        JsonSerializer.Serialize(writer, fieldValue, options);
                    }
                }

                writer.WriteEndObject();
            }
        }
    }

}

public class CustomDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.ParseExact(reader.GetString()!, Format, System.Globalization.CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString(Format));
    }
}