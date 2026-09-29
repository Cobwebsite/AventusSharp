using System;
using System.Linq.Expressions;
using System.Reflection;

namespace AventusSharp.Data.Manager.DB;

internal sealed class PreparedExternalExpression<T> : ExpressionVisitor where T : IStorable
{
    private readonly DatabaseGenericBuilder<T> builder;
    private readonly ParameterExpression parameter;

    private PreparedExternalExpression(DatabaseGenericBuilder<T> builder, ParameterExpression parameter)
    {
        this.builder = builder;
        this.parameter = parameter;
    }

    public static Expression<Func<T, bool>> Rewrite(DatabaseGenericBuilder<T> builder, Expression<Func<T, bool>> expression)
    {
        PreparedExternalExpression<T> visitor = new(builder, expression.Parameters[0]);
        return Expression.Lambda<Func<T, bool>>(visitor.Visit(expression.Body)!, expression.Parameters);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ConstantExpression constant && constant.Value != null && node.Expression != parameter)
        {
            string name = node.Member.Name;
            if (!builder.WhereParamsInfo.ContainsKey(name))
            {
                builder.WhereParamsInfo[name] = new ParamsInfo
                {
                    Name = name,
                    TypeLvl0 = node.Type
                };
            }
            MethodInfo getter = typeof(DatabaseGenericBuilder<T>).GetMethod(nameof(DatabaseGenericBuilder<>.GetPreparedExternalValue))!;

            return Expression.Convert(
                Expression.Call(Expression.Constant(builder), getter, Expression.Constant(name)),
                node.Type
            );
        }
        return base.VisitMember(node);
    }
}
