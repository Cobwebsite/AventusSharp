using System;

namespace AventusSharp.Tools;


public static class LongExtension
{
    public static long Max(this long value) => value;
    public static long Min(this long value) => value;
    public static long Abs(this long value) => Math.Abs(value);
}

public static class IntExtension
{
    public static int Max(this int value) => value;
    public static int Min(this int value) => value;
    public static int Abs(this int value) => Math.Abs(value);
}
public static class ShortExtension
{
    public static short Max(this short value) => value;
    public static short Min(this short value) => value;
    public static short Abs(this short value) => Math.Abs(value);
}


public static class DoubleExtension
{
    public static double Max(this double value) => value;
    public static double Min(this double value) => value;
    public static double Abs(this double value) => Math.Abs(value);
    public static double Round(this double value) => Math.Round(value);
    public static double Ceil(this double value) => Math.Ceiling(value);
    public static double Floor(this double value) => Math.Floor(value);
}

public static class FloatExtension
{
    public static float Max(this float value) => value;
    public static float Min(this float value) => value;
    public static float Abs(this float value) => Math.Abs(value);
    public static decimal Round(this float value) => Math.Round((decimal)value);
    public static decimal Ceil(this float value) => Math.Ceiling((decimal)value);
    public static decimal Floor(this float value) => Math.Floor((decimal)value);
}

public static class DecimalExtension
{
    public static decimal Max(this decimal value) => value;
    public static decimal Min(this decimal value) => value;
    public static decimal Abs(this decimal value) => Math.Abs(value);
    public static decimal Round(this decimal value) => Math.Round((decimal)value);
    public static decimal Ceil(this decimal value) => Math.Ceiling((decimal)value);
    public static decimal Floor(this decimal value) => Math.Floor((decimal)value);
}
