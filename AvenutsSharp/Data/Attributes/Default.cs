using System;

namespace AventusSharp.Data.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class Default : Attribute
{
    public object Value { get; }
    public Default(object o)
    {
        Value = o;
    }
}
