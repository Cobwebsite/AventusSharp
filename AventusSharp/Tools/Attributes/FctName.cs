using System;

namespace AventusSharp.Tools.Attributes
{
    /// <summary>
    /// Define the name for your function inside your typescript
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
}
