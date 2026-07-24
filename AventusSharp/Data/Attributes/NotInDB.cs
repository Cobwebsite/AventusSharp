using System;

namespace AventusSharp.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NotInDB : Attribute, IAvoidDependance
    {
    }
}
