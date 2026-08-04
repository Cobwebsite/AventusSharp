namespace AventusSharp.Chart;

[AttributeUsage(AttributeTargets.Class)]
public class Diagram : Attribute
{
    public readonly string? Name;
    public readonly string? Area;
    public readonly string? AreaColor;
    public readonly string? TableColor;

    public Diagram(string? name = null, string? area = null, string? areaColor = null, string? tableColor = null)
    {
        Name = name;
        Area = area;
        AreaColor = areaColor;
        TableColor = tableColor;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DiagramRelation : Attribute
{
    public readonly string Description;

    public DiagramRelation(string description)
    {
        Description = description;
    }
}
