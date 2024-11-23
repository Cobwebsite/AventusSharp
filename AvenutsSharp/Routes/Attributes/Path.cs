using System;
using System.Text.RegularExpressions;

namespace AventusSharp.Routes.Attributes
{

    [AttributeUsage(AttributeTargets.Method)]
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

    [AttributeUsage(AttributeTargets.Method)]
    public class PathRegex : Path
    {
        public PathRegex(string pattern) : base("°" + pattern + "°")
        {
        }
    }
}
