using System;

namespace AventusSharp.Tools.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface)]
    public class Export : Attribute
    {
        public string? Namespace;
        public bool? Internal;

        public Export() { }

        public Export(string _namespace)
        {
            Namespace = _namespace;
        }

        public Export(bool _internal)
        {
            Internal = _internal;
        }

        public Export(string _namespace, bool _internal)
        {
            Namespace = _namespace;
            Internal = _internal;
        }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class NoExport : Attribute
    {

    }
}
