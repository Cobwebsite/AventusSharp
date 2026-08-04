namespace AventusSharp.Tools;

public static class TypeExtension
{
    public static bool IsNullable(this Type type) => Nullable.GetUnderlyingType(type) != null;
}
