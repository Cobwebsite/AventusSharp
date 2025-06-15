using System;

namespace AventusSharp.Routes.Form
{
    public static class Tools
    {
        public static object? DefaultValue(Type type)
        {
            object? value = null;
            if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                value = Activator.CreateInstance(type);
            }
            return value;
        }

    }
}
