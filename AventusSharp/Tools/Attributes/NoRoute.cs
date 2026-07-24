using System;

namespace AventusSharp.Tools.Attributes
{
    /// <summary>
    /// Define that the function isn't a route
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class NoRoute : Attribute
    {
        public NoRoute()
        {
        }
    }
}
