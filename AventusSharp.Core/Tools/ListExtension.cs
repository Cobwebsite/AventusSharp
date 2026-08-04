using System.Collections;

namespace AventusSharp.Tools;

public static class ListExtension
{
    public static List<T> ConvertList<T>(this IList value) => value.ToList<T>();

    public static List<T> ToList<T>(this IList value)
    {
        List<T> result = new();
        foreach (object item in value)
        {
            if (item is T casted)
            {
                result.Add(casted);
            }
        }
        return result;
    }

    public static IList ToListOfType(this IList value, Type elementType)
    {
        Type resultType = typeof(List<>).MakeGenericType(elementType);
        IList result = (IList)Activator.CreateInstance(resultType)!;
        foreach (object item in value)
        {
            if (item != null && elementType.IsAssignableFrom(item.GetType()))
            {
                result.Add(item);
            }
        }
        return result;
    }
}
