using System;

namespace AventusSharp.Tools.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property)]
    public class Export : Attribute
    {
        public string? Namespace;
        public bool? Internal;

        public string? DefaultValue;

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

        public Export(string? _namespace = null, bool _internal = false, string? defaultValue = null)
        {
            Namespace = _namespace;
            Internal = _internal;
            DefaultValue = defaultValue;
        }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class NoExport : Attribute
    {

    }
}
