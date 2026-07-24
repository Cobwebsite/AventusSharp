using System;
using System.Reflection;
using AventusSharp.Tools.Attributes;
using Microsoft.Extensions.Configuration;
using Converter = System.Convert;

namespace AventusSharp.Tools
{
    public abstract class AutoConfiguration
    {

        protected AutoConfiguration(IConfiguration configuration)
        {
            LoadConfiguration(configuration);
        }

        private void LoadConfiguration(IConfiguration configuration)
        {
            Type configurationType = GetType();

            foreach (PropertyInfo property in configurationType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetCustomAttribute<ConfigIgnore>() != null)
                    continue;

                if (!property.CanWrite)
                    continue;

                ConfigSection? sectionAttribute = property.GetCustomAttribute<ConfigSection>();

                string sectionName = sectionAttribute?.Name ?? property.Name;

                IConfigurationSection section = configuration.GetSection(sectionName);

                object value = section.Read(property.PropertyType);

                property.SetValue(this, value);
            }

            foreach (FieldInfo field in configurationType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.GetCustomAttribute<ConfigIgnore>() != null)
                    continue;


                ConfigSection? sectionAttribute = field.GetCustomAttribute<ConfigSection>();

                string sectionName = sectionAttribute?.Name ?? field.Name;

                IConfigurationSection section = configuration.GetSection(sectionName);

                object value = section.Read(field.FieldType);

                field.SetValue(this, value);
            }
        }
    }

    public static class ConfigurationExtension
    {
        public static T Read<T>(this IConfiguration configuration)
        {
            return (T)configuration.Read(typeof(T));
        }

        public static object Read(this IConfiguration configuration, Type configurationType)
        {
            object? result = configuration.Get(configurationType);

            result ??= Activator.CreateInstance(configurationType);

            if (result == null)
            {
                throw new InvalidOperationException(
                    $"Unable to create configuration type " +
                    configurationType.FullName
                );
            }

            ApplyEnvironmentVariables(result, configurationType);

            return result;
        }


        private static void ApplyEnvironmentVariables(object result, Type configurationType)
        {
            foreach (PropertyInfo property in configurationType.GetProperties())
            {
                EnvName? envName = property.GetCustomAttribute<EnvName>();

                if (envName == null || !property.CanWrite)
                {
                    continue;
                }

                string? envValue = Environment.GetEnvironmentVariable(envName.Name);

                if (envValue == null)
                {
                    continue;
                }

                Type valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                object? convertedValue = ConvertValue(envValue, valueType);

                property.SetValue(result, convertedValue);
            }

            foreach (FieldInfo field in configurationType.GetFields())
            {
                EnvName? envName = field.GetCustomAttribute<EnvName>();

                if (envName == null)
                {
                    continue;
                }

                string? envValue = Environment.GetEnvironmentVariable(envName.Name);

                if (envValue == null)
                {
                    continue;
                }

                Type valueType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

                object? convertedValue = ConvertValue(envValue, valueType);

                field.SetValue(result, convertedValue);
            }

        }

        private static object? ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
            {
                return value;
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value, ignoreCase: true);
            }

            return Converter.ChangeType(value, targetType);
        }
    }
}