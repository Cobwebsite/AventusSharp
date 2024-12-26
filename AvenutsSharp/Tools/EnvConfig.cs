using System;
using System.Reflection;
using AventusSharp.Tools.Attributes;

namespace AventusSharp.Tools
{
    public class EnvConfig
    {
        public static T? Load<T>()
        {
            Type t = typeof(T);
            T? config = (T?)Activator.CreateInstance(t);

            if (config == null) return default;

            PropertyInfo[] properties = t.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                EnvName? envName = property.GetCustomAttribute<EnvName>();
                if (envName != null)
                {
                    string? envValue = Environment.GetEnvironmentVariable(envName.Name);
                    Type valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    object? convertedValue = envValue != null ? Convert.ChangeType(envValue, valueType) : null;
                    property.SetValue(config, convertedValue);
                }
            }

            FieldInfo[] fields = t.GetFields();
            foreach (FieldInfo field in fields)
            {
                EnvName? envName = field.GetCustomAttribute<EnvName>();
                if (envName != null)
                {
                    string? envValue = Environment.GetEnvironmentVariable(envName.Name);
                    Type valueType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
                    object? convertedValue = envValue != null ? Convert.ChangeType(envValue, valueType) : null;
                    field.SetValue(config, convertedValue);
                }
            }

            return config;
        }
    }
}