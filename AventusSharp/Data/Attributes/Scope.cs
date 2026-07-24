using System;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;

namespace AventusSharp.Data.Attributes;


public interface IScope
{
    public Expression<Func<object, bool>>? Where(HttpContext? context);
}
[AttributeUsage(AttributeTargets.Class)]
public abstract class Scope<T> : Attribute, IScope
{
    public abstract Expression<Func<T, bool>>? Where(HttpContext? context);

    Expression<Func<object, bool>>? IScope.Where(HttpContext? context)
    {
        var originalExpression = Where(context);
        if(originalExpression == null) return null;

        var objectParam = Expression.Parameter(typeof(object), "o");

        var castedParam = Expression.Convert(objectParam, typeof(T));

        var body = Expression.Invoke(originalExpression, castedParam);

        return Expression.Lambda<Func<object, bool>>(body, objectParam);
    }
}

