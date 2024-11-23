using System;

namespace AventusSharp.Data.Attributes
{
    public enum SizeEnum
    {
        MaxVarChar,
        Text,
        MediumText,
        LongText
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class Size : ValidationAttribute
    {
        public int Max { get; private set; } = 255;
        public int Min { get; private set; }

        public SizeEnum? SizeType { get; private set; } = null;
        private string Msg;

        public Size(int min, int max, string msg = "")
        {
            Min = min;
            Max = max;
            Msg = msg;
        }

        public Size(int max, string msg = "") : this(0, max, msg) { }

        public Size(int min, SizeEnum max, string msg = "")
        {
            Min = min;
            SizeType = max;
            Msg = msg;
        }
        public Size(SizeEnum max, string msg = "") : this(0, max, msg) { }


        public override ValidationResult IsValid(object? value, ValidationContext context)
        {
            if (value is string casted)
            {
                if (SizeType == null)
                {
                    if (casted.Length > Max || casted.Length < Min)
                    {
                        string msg = Msg == "" ? $"The size of the field {context.FieldName} must be between {Min} and {Max} chars." : Msg;
                        return new ValidationResult(msg, context.FieldName);
                    }
                }
                else
                {
                    if (casted.Length < Min)
                    {
                        string msg = Msg == "" ? $"The size of the field {context.FieldName} must be greater than {Min} chars." : Msg;
                        return new ValidationResult(msg, context.FieldName);
                    }
                }
            }
            return ValidationResult.Success;
        }
    }
}
