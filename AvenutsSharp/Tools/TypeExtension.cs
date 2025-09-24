
using System;
using System.Linq;
using System.Reflection;

namespace AventusSharp.Tools;

public static class TypeExtension
{
    public static bool IsNullable(this Type type)
    {
        Type? typeIfNullable = System.Nullable.GetUnderlyingType(type);
        return typeIfNullable != null;
    }

}