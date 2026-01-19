using System;
using System.Linq.Expressions;

namespace AventusSharp.Data.Attributes;


public interface IScope
{
    public Expression<Func<object, bool>> Where();
}
[AttributeUsage(AttributeTargets.Class)]
public abstract class Scope<T> : Attribute, IScope
{
    public abstract Expression<Func<T, bool>> Where();

    Expression<Func<object, bool>> IScope.Where()
    {
        var originalExpression = Where();

        var objectParam = Expression.Parameter(typeof(object), "o");

        var castedParam = Expression.Convert(objectParam, typeof(T));

        var body = Expression.Invoke(originalExpression, castedParam);

        return Expression.Lambda<Func<object, bool>>(body, objectParam);
    }
}

