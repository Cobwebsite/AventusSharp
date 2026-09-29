using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Storage.Default;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager.DB;

internal sealed class ExternalExpressionLoader<T> : ExpressionVisitor where T : IStorable
{
    private readonly DatabaseGenericBuilder<T> builder;
    private readonly ParameterExpression parameter;
    private readonly HashSet<string> loaded = new();
    private readonly List<List<LambdaStep>> paths = new();
    public bool HasExternal { get; private set; }

    private ExternalExpressionLoader(DatabaseGenericBuilder<T> builder, ParameterExpression parameter)
    {
        this.builder = builder;
        this.parameter = parameter;
    }

    public static bool Load(DatabaseGenericBuilder<T> builder, LambdaExpression expression)
    {
        ExternalExpressionLoader<T> loader = new ExternalExpressionLoader<T>(builder, expression.Parameters[0]);
        loader.Visit(expression.Body);
        foreach (var steps in loader.paths)
        {
            if (!IsLocalManyToMany(builder, steps))
            {
                LambdaIncludeResult prepareLambda = builder.LambdaInclude(steps, null, false);
                if (prepareLambda.IsExternal)
                {
                    loader.HasExternal = true;
                }
            }
        }
        return loader.HasExternal;
    }

    public static bool RequiresExternal(DatabaseGenericBuilder<T> builder, LambdaExpression expression)
    {
        ExternalExpressionLoader<T> loader = new ExternalExpressionLoader<T>(builder, expression.Parameters[0]);
        loader.Visit(expression.Body);
        return loader.paths.Any(steps => IsExternalPath(builder, steps));
    }

    private static bool IsExternalPath(DatabaseGenericBuilder<T> builder, List<LambdaStep> steps)
    {
        TableInfo? table = builder.InfoByPath[""].TableInfo;
        foreach (LambdaStep step in steps)
        {
            if (table == null)
                return false;

            TableMemberInfoSql? member = null;
            TableReverseMemberInfo? reverse = null;
            TableInfo? current = table;
            while (current != null)
            {
                if (member == null)
                {
                    member = current.Members.FirstOrDefault(candidate => candidate.Name == step.Name);
                }
                if (reverse == null)
                {
                    reverse = current.ReverseMembers.FirstOrDefault(candidate => candidate.Name == step.Name);
                }
                if (reverse != null)
                {
                    break;
                }
                current = current.Parent;
            }
            if (reverse != null)
                return true;

            if (member == null)
                return false;

            if (member is not ITableMemberInfoSqlLink link)
                return false;

            if (builder.DM is not IDatabaseDM manager || !manager.IsSameStorage(member.DM))
                return true;

            table = link.TableLinked;

            if (table == null && link.TableLinkedType != null)
            {
                table = builder.Storage.GetTableInfo(link.TableLinkedType);
            }
        }
        return false;
    }

    public static void LoadFields(DatabaseGenericBuilder<T> builder, LambdaExpression expression)
    {
        ExternalExpressionLoader<T> loader = new ExternalExpressionLoader<T>(builder, expression.Parameters[0]);
        loader.Visit(expression.Body);
        foreach (var steps in loader.paths)
        {
            if (!IsLocalManyToMany(builder, steps))
            {
                builder.LambdaInclude(steps, null, true);
            }
        }
    }

    private static bool IsLocalManyToMany(DatabaseGenericBuilder<T> builder, List<LambdaStep> steps)
    {
        TableMemberInfoSql? member = builder.InfoByPath[""].GetTableMemberInfo(steps[0].Name);
        if (member is not ITableMemberInfoSqlLinkMultiple multiple)
        {
            return false;
        }
        if (multiple.TableLinked == null)
        {
            return false;
        }
        if (builder.DM is not IDatabaseDM manager)
        {
            return false;
        }
        if (!manager.IsSameStorage(member.DM))
        {
            return false;
        }
        return true;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        List<MemberExpression> members = new();
        Expression? current = node;
        while (current is MemberExpression member)
        {
            members.Insert(0, member);
            current = member.Expression;
            while (
                current is UnaryExpression unary &&
                (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked)
            )
            {
                current = unary.Operand;
            }
        }

        if (current == parameter)
        {
            List<LambdaStep> steps = new();
            foreach (MemberExpression member in members)
            {
                if (steps.Count > 0 && !typeof(IStorable).IsAssignableFrom(steps[^1].Type))
                    break;

                steps.Add(
                    new LambdaStep
                    {
                        Name = member.Member.Name,
                        Type = member.Type
                    }
                );
            }
            
            string path = string.Join(".", steps.Select(step => step.Name));

            if (steps.Count > 0 && loaded.Add(path))
            {
                paths.Add(steps);
            }
        }
        return base.VisitMember(node);
    }
}
