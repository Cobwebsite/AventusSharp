using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System;

namespace AventusSharp.Routes.Attributes
{

    [AttributeUsage(AttributeTargets.Class)]
    public class Prefix : Attribute
    {
        public string txt { get; private set; }
        public Prefix(string txt)
        {
            if (!txt.StartsWith("/"))
            {
                txt = "/" + txt;
            }
            if (txt.EndsWith("/") && txt != "/")
            {
                txt = txt.TrimEnd('/');
            }
            this.txt = txt;
        }
    }
}
