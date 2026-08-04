namespace AventusSharp.Tools.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property)]
public class Export : Attribute
{
    public string? Namespace;
    public bool? Internal;
    public string? DefaultValue;

    public Export()
    {
    }

    public Export(string @namespace)
    {
        Namespace = @namespace;
    }

    public Export(bool @internal)
    {
        Internal = @internal;
    }

    public Export(string @namespace, bool @internal)
    {
        Namespace = @namespace;
        Internal = @internal;
    }

    public Export(string? @namespace = null, bool @internal = false, string? defaultValue = null)
    {
        Namespace = @namespace;
        Internal = @internal;
        DefaultValue = defaultValue;
    }
}

[AttributeUsage(AttributeTargets.All)]
public class NoExport : Attribute
{
}
