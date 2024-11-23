using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace AventusSharp.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public abstract class ValidationAttribute : System.Attribute
    {
        public abstract ValidationResult IsValid(object? value, ValidationContext context);
    }

    public class ValidationContext
    {
        public string FieldName { get; set; }
        public Type FieldType { get; set; }
        public Type? ReflectedType { get; set; }
        public TableInfo TableInfo { get; set; }

        public IStorable? Item { get; set; }

        public ValidationContext(string fieldName, Type fieldType, Type? reflectedType, TableInfo tableInfo, IStorable? item)
        {
            FieldName = fieldName;
            FieldType = fieldType;
            ReflectedType = reflectedType;
            TableInfo = tableInfo;
            Item = item;
        }
    }

    public class ValidationResult
    {
        private static ValidationResult success = new();
        public static ValidationResult Success { get => success; set => success = value; }

        public List<GenericError> Errors { get; set; } = new List<GenericError>();

        public ValidationResult()
        {
        }
        public ValidationResult(string msg, string? fieldName = null)
        {
            DataError error = new DataError(DataErrorCode.ValidationError, msg);
            if (fieldName != null)
                error.Details.Add(new FieldErrorInfo(fieldName));
            Errors.Add(error);
        }
    }

}
