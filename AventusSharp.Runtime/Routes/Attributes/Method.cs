using System;

namespace AventusSharp.Routes.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Get : Attribute
    {
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class Post : Attribute
    {
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class Put : Attribute
    {
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class Delete : Attribute
    {
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class Options : Attribute
    {
    }
}
