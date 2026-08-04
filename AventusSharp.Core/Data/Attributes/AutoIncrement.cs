using System;

namespace AventusSharp.Data.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class AutoIncrement : System.Attribute
{
    public bool Value {get; protected set;}

    public AutoIncrement() {
        Value = true;
    }

    public AutoIncrement(bool value) {
        Value = value;
    }
}
