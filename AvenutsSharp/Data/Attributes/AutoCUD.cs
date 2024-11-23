using System;

namespace AventusSharp.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AutoCUD : System.Attribute
    {
        public readonly bool Is;
        public AutoCUD(bool apply = true)
        {
            Is = apply;
        }
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AutoCRUD : System.Attribute
    {
        public readonly bool Is;
        public AutoCRUD(bool apply = true)
        {
            Is = apply;
        }
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AutoCreate : System.Attribute
    {
        public readonly bool Is;
        public AutoCreate(bool apply = true)
        {
            Is = apply;
        }
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AutoUpdate : System.Attribute
    {
        public readonly bool Is;
        public AutoUpdate(bool apply = true)
        {
            Is = apply;
        }
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AutoDelete : System.Attribute
    {
        public readonly bool Is;
        public AutoDelete(bool apply = true)
        {
            Is = apply;
        }
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AutoRead : System.Attribute
    {
        public readonly bool Is;
        public AutoRead(bool apply = true)
        {
            Is = apply;
        }
    }
}
