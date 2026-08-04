using System;

namespace AventusSharp.Data.Attributes
{
   
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class)]
    public class SqlName : Attribute
    {

        public string Name { get; private set; }

        public SqlName(string name)
        {
            Name = name;
        }

    }
}
