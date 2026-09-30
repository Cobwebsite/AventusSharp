using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AventusSharp.Data.Storage.Default.TableMember;

/// <summary>Stores ordinary lists and dictionaries of scalar values in one JSON column.</summary>
public sealed class PrimitiveCollectionTableMember : CustomTableMember
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };


    public static bool Supports(Type type)
    {
        if (!type.IsGenericType) return false;

        Type definition = type.GetGenericTypeDefinition();
        Type[] arguments = type.GetGenericArguments();
        if (definition == typeof(List<>))
        {
            return IsScalar(arguments[0]);
        }
        if (definition == typeof(Dictionary<,>))
        {
            if (arguments[0] != typeof(string) && arguments[0] != typeof(int))
            {
                return false;
            }
            return IsScalar(arguments[1]);
        }
        return false;
    }

    private static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (typeof(IStorable).IsAssignableFrom(type)) return false;
        if (type.IsEnum) return true;
        if (GetDbType(type, null) != null) return true;
        return false;
    }

    public PrimitiveCollectionTableMember(MemberInfo memberInfo, TableInfo tableInfo, bool isNullable)
        : base(memberInfo, tableInfo, isNullable)
    {
    }


    public override DbType? GetDbType() => DbType.String;

    public override object? GetSqlValue(object obj)
    {
        object? value = GetValue(obj);
        return value == null ? null : JsonSerializer.Serialize(value, MemberType, JsonOptions);
    }

    protected override void SetSqlValue(object obj, string? value)
    {
        if (value == null)
        {
            SetValue(obj, null);
            return;
        }
        SetValue(obj, JsonSerializer.Deserialize(value, MemberType, JsonOptions));
    }
}
