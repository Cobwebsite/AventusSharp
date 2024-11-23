using System;

namespace AventusSharp.Data.Attributes
{

    /**
     * Use it to add a link to your database (works only on int)
     */
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ForeignKey : Attribute
    {
        public Type Type { get; private set; }
        public ForeignKey(Type type)
        {
            Type = type;
        }
    }

    /**
     * Use it to add a link to your database (works only on int)
     */
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ForeignKey<T> : ForeignKey where T : IStorable
    {
        public ForeignKey() : base(typeof(T))
        {
        }
    }
}
