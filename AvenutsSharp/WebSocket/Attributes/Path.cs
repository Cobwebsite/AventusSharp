using System;

namespace AventusSharp.WebSocket.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class Path : Attribute
    {
        public string pattern { get; private set; }
        public Path(string pattern)
        {
            if (!pattern.StartsWith("/"))
            {
                pattern = "/" + pattern;
            }
            if (pattern.EndsWith("/"))
            {
                pattern = pattern.TrimEnd('/');
            }
            this.pattern = pattern;
        }
    }
}
