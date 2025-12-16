using System;
using System.Collections.Generic;
using AventusSharp.Data.Storage.Default;

namespace AventusSharp.Data.Attributes
{
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class)]
    public abstract class Storage : Attribute
    {
        internal Type type;
        public Storage(Type type)
        {
            this.type = type;
        }

    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class)]
    public class Storage<T> : Storage
    {
        public Storage() : base(typeof(T))
        {
        }

    }
}
