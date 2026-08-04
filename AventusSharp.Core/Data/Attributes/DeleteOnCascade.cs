using System;

namespace AventusSharp.Data.Attributes
{
    /// <summary>
    /// When the field below will be deleting the current item will be deleted too
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DeleteOnCascade : System.Attribute
    {
    }

    /// <summary>
    /// When the field below will be deleting the current field will be set to null
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DeleteSetNull : System.Attribute
    {
    }

    // /// <summary>
    // /// When the field below will be deleting the current field will be set to null
    // /// </summary>
    // [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    // public class UpdateOnCascade : System.Attribute
    // {
    // }
}
