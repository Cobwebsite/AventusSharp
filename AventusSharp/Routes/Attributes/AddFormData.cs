using System;

namespace AventusSharp.Routes.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class AddFormData : Attribute
{
    public bool Value { get; private set; }
    public AddFormData()
    {
        Value = true;
    }
    public AddFormData(bool value)
    {
        Value = value;
    }
}