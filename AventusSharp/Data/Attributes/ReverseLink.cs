using System;
using System.Linq.Expressions;

namespace AventusSharp.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ReverseLink : Attribute, IAvoidDependance
    {
        public string? field;
        public ReverseLink(string? field = null)
        {
            this.field = field;
        }
    }
}
