using System;

namespace AventusSharp.Chart;

[AttributeUsage(AttributeTargets.Class)]
public class Diagram : Attribute
{
    public readonly string? Name;
    public readonly string? Area;
    public Diagram(string? name = null, string? area = null)
    {
        Name = name;
        Area = area;
    }
}