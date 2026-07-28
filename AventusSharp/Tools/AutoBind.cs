using System;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace AventusSharp.Tools;

public static class Binder
{
    public static T AutoBind<T>(object source)
    {
        T o = (T)TypeTools.CreateNewObj(typeof(T));
        AutoBind(source, o);
        return o;
    }
    public static object AutoBind(object source, Type target)
    {
        object o = TypeTools.CreateNewObj(target);
        AutoBind(source, o);
        return o;
    }
    public static void AutoBind(object source, object target)
    {
        if (source == null) return;

        Type destinationType = target.GetType();
        Type sourceType = source.GetType();

        PropertyInfo[] destProps = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo destProp in destProps)
        {
            if (!destProp.CanWrite) continue;

            Bind? bindAttr = destProp.GetCustomAttribute<Bind>();
            string sourceName = bindAttr?.Name ?? destProp.Name;

            PropertyInfo? sourceProp = sourceType.GetProperty(sourceName);
            if (sourceProp == null || !sourceProp.CanRead) continue;

            object? value = sourceProp.GetValue(source);
            if (value == null) continue;

            try
            {
                object? finalValue;
                Convert? convertAttr = destProp.GetCustomAttribute<Convert>();

                if (convertAttr != null)
                {
                    finalValue = convertAttr.Transform(value);
                }
                else if (!destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                {
                    finalValue = System.Convert.ChangeType(value, destProp.PropertyType);
                }
                else
                {
                    finalValue = value;
                }

                destProp.SetValue(target, finalValue);
            }
            catch (Exception ex)
            {
                AventusLogger.Instance.LogError(ex, "AutoBinding failed");
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class Bind : Attribute
{
    public string? Name { get; }
    public Bind(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Property)]
public abstract class Convert : Attribute
{
    public Convert()
    {

    }

    public abstract object? Transform(object from);
}

[AttributeUsage(AttributeTargets.Property)]
public abstract class Convert<T, U> : Convert
{
    public abstract U Transform(T from);
    public override object? Transform(object from)
    {
        if (from is T t)
        {
            return Transform(t);
        }
        return null;
    }
}
