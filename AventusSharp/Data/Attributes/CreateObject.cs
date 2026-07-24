using System;

namespace AventusSharp.Data.Attributes
{
    /// <summary>
    /// Set it over an Foreignkey to create the object field linked to the id during export to AventusJs
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class CreateObject : Attribute
    {
        public string? Name { get; private set; }

        public CreateObject() { }

        public CreateObject(string name)
        {
            Name = name;
        }
    }

}
