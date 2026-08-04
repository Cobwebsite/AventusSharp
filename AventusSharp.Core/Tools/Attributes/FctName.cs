namespace AventusSharp.Tools.Attributes;

/// <summary>
/// Defines the name of a function in the generated TypeScript code.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class FctName : Attribute
{
    public string name { get; private set; }

    public FctName(string name)
    {
        this.name = name;
    }
}
