using System;

namespace AventusSharp.Data.Attributes
{
    /// <summary>
    /// Attribute used to allow a null value for the Property
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class Nullable : Attribute
    {
    }

    /// <summary>
    /// Attribute used to allow a null value for the Property
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NotNullable : ValidationAttribute
    {
        private string Msg;
        public NotNullable()
        {
            this.Msg = "";
        }
        public NotNullable(string msg)
        {
            this.Msg = msg;
        }
        public override ValidationResult IsValid(object? value, ValidationContext context)
        {
            if(value == null)
            {
                string msg = this.Msg == "" ? $"The field {context.FieldName} is required." : this.Msg;
                return new ValidationResult(msg, context.FieldName);
            }
            return ValidationResult.Success;
        }
    }
}
