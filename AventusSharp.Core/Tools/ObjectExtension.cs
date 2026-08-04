namespace AventusSharp.Tools;

public static class ObjectExtension
{
    public static bool IsNumber(this object value)
    {
        return value is sbyte
            or byte
            or short
            or ushort
            or int
            or uint
            or long
            or ulong
            or float
            or double
            or decimal;
    }
}
