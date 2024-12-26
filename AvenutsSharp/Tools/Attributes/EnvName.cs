using System;

namespace AventusSharp.Tools.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class EnvName : Attribute
    {
        internal string Name { get; private set; }
        public EnvName(string name)
        {
            Name = name;
        }
    }

}