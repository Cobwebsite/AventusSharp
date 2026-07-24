using System;

namespace AventusSharp.Tools.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigSection : Attribute
{
    public string Name { get; }

    public ConfigSection(string name)
    {
        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class ConfigIgnore : Attribute
{
}