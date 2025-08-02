using System;
using System.Collections;
using System.Collections.Generic;

namespace AventusSharp.Tools;

public static class ListExtension
{
    public static List<Y> ToList<Y>(this IList value)
    {
        List<Y> result = new();
        foreach (object item in value)
        {
            if (item is Y casted)
            {
                result.Add(casted);
            }
        }
        return result;
    }

    public static IList ToListOfType(this IList value, Type y)
    {
        var resultType = typeof(List<>).MakeGenericType(y);
        var result = (IList)Activator.CreateInstance(resultType)!;

        foreach (object item in value)
        {
            if (item != null && y.IsAssignableFrom(item.GetType()))
            {
                result.Add(item);
            }
        }

        return result;
    }
}

