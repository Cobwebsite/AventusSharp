using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AventusSharp.Data.Attributes;
using AventusSharp.Tools;

namespace AventusSharp.Data.Manager.DB;


public abstract class DatabaseSubBuilder
{
    public static DatabaseSubBuilder Make(Type typeFrom, Type typeTo)
    {
        Type genericType = typeof(DatabaseSubBuilder<,>).MakeGenericType(typeFrom, typeTo);

        object instance = Activator.CreateInstance(
            genericType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            null,
            null)!;

        return (DatabaseSubBuilder)instance;
    }


    public abstract Task<VoidWithError> Run<X>(List<X> items) where X : notnull;

    public abstract VoidWithError PrepareReverseLink(List<string> names, List<LambdaExpression>? fields);
    public abstract VoidWithError ExtendReverseLink(List<string> names, List<LambdaExpression>? fields);

    public abstract VoidWithError PrepareExternalStorage(List<string> names, List<LambdaExpression>? fields);

    public abstract VoidWithError ExtendExternalStorage(List<string> names, List<LambdaExpression>? fields);
}
public enum DatabaseSubBuilderKind
{
    NotDefined,
    ReverseLink,
    ExternalStorage
}
public class DatabaseSubBuilder<X, Y> : DatabaseSubBuilder where X : IStorable where Y : IStorable
{



    public DatabaseSubBuilderKind Kind = DatabaseSubBuilderKind.NotDefined;
    public IGenericDM dmX;
    public IGenericDM dmY;

    private DatabaseSubBuilder()
    {
        dmX = GenericDM.Get<X>();
        dmY = GenericDM.Get<Y>();
    }

    public override Task<VoidWithError> Run<T>(List<T> items)
    {
        return Run(items.ToList<X>());
    }
    public async Task<VoidWithError> Run(List<X> items)
    {
        VoidWithError result = new VoidWithError();
        if (Kind == DatabaseSubBuilderKind.ReverseLink)
        {
            await result.RunAsync(() => RunReverseLink(items));
        }
        else if (Kind == DatabaseSubBuilderKind.ExternalStorage)
        {
            await result.RunAsync(() => RunExternalStorage(items));
        }
        return result;
    }

    #region Revers Link
    private QueryBuilderPrepared<Y>? ReverseLinkQuery;
    private DataMemberInfo? ReverseLinkMemberX;
    private DataMemberInfo? ReverseLinkReverseMember;
    private async Task<VoidWithError> RunReverseLink(List<X> items)
    {
        VoidWithError result = new VoidWithError();
        if (ReverseLinkQuery == null || ReverseLinkMemberX == null || ReverseLinkReverseMember == null)
        {
            result.Errors.Add(new DataError(DataErrorCode.ReverseLinkNotPrepared, "The ReverseLink isn't prepared, please open an issue"));
            return result;
        }
        Dictionary<int, List<X>> elements = new();
        foreach (X item in items)
        {
            if (!elements.ContainsKey(item.Id))
            {
                elements[item.Id] = new();
            }
            elements[item.Id].Add(item);
        }

        var query = ReverseLinkQuery.New();
        if (ReverseLinkReverseMember.IsNullable)
        {
            List<int?> ids = elements.Keys.Select(p => (int?)p).ToList();
            query.Prepare(ids);
        }
        else
        {
            List<int> ids = elements.Keys.ToList();
            query.Prepare(ids);
        }

        List<Y>? linkedElement = await result.ExtractAsync(query.RunWithError);
        if (linkedElement == null) return result;
        DataMemberInfo memberX = ReverseLinkMemberX;
        DataMemberInfo reverseMember = ReverseLinkReverseMember;


        foreach (Y item in linkedElement)
        {
            object? reverseItem = reverseMember.GetValue(item);
            List<X> elementList = new();
            if (reverseItem is int reverseId)
            {
                if (elements.ContainsKey(reverseId))
                    elementList = elements[reverseId];
            }
            else if (reverseItem is IStorable reverseItem2)
            {
                if (elements.ContainsKey(reverseItem2.Id))
                    elementList = elements[reverseItem2.Id];
            }
            foreach (X element in elementList)
            {
                object? list = memberX.GetValue(element);
                if (list is null)
                {
                    bool isList = memberX.Type?.GetInterfaces().Contains(typeof(IList)) ?? false;
                    if (isList)
                    {
                        list = Activator.CreateInstance(memberX.Type!);
                        memberX.SetValue(element, list);
                    }
                }
                if (list is IList Ilist)
                {
                    Ilist.Add(item);
                }
                else
                {
                    try
                    {
                        memberX.SetValue(element, item);
                    }
                    catch (Exception e)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                    }
                }
            }
        }
        return result;

    }
    public override VoidWithError PrepareReverseLink(List<string> names, List<LambdaExpression>? fields)
    {
        Kind = DatabaseSubBuilderKind.ReverseLink;
        string name = names[0];
        VoidWithError result = new VoidWithError();
        ResultWithError<DataMemberInfo> memberXQuery = dmX.GetMemberInfo<X>(name);
        if (memberXQuery.Result != null && memberXQuery.Success)
        {
            ReverseLinkMemberX = memberXQuery.Result;
            ReverseLink? reverseLinkAttr = memberXQuery.Result.GetCustomAttribute<ReverseLink>();
            if (reverseLinkAttr == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.ReverseLinkNotExist, "The field " + memberXQuery.Result.Name + " isn't a ReverseLink"));
                return result;
            }

            string? reverseName = reverseLinkAttr.field;
            DataMemberInfo? reverseMember = null;
            if (reverseName != null)
            {
                ResultWithError<DataMemberInfo> memberYQuery = dmY.GetMemberInfo<Y>(reverseName);

                if (memberYQuery.Result != null && memberYQuery.Success)
                {
                    reverseMember = memberYQuery.Result;
                }
                else
                {
                    result.Errors.AddRange(memberYQuery.Errors);
                    result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, "The name " + reverseName + " can't be found on " + TypeTools.GetReadableName(typeof(Y))));
                }
            }
            else
            {
                ResultWithError<List<DataMemberInfo>> membersYQuery = dmY.GetMembersInfo<Y, X>();
                if (membersYQuery.Result != null)
                {
                    membersYQuery.Result = membersYQuery.Result.Where(p => p.GetCustomAttribute<NotInDB>() == null).ToList();
                }
                if (membersYQuery.Result != null && membersYQuery.Success)
                {
                    if (membersYQuery.Result.Count > 1)
                    {
                        result.Errors.Add(
                            new DataError(
                                DataErrorCode.TooMuchMemberFound,
                                "Too much matching type " + TypeTools.GetReadableName(typeof(X)) + " on type " + TypeTools.GetReadableName(typeof(Y)) + ". Please define a name (" + string.Join(", ", membersYQuery.Result.Select(s => s.Name)) + ")"
                            )
                        );
                    }
                    else if (membersYQuery.Result.Count == 0)
                    {
                        membersYQuery = dmY.GetMembersInfo<Y, int>();
                        if (membersYQuery.Result != null && membersYQuery.Success)
                        {
                            membersYQuery.Result = membersYQuery.Result.Where(p => p.GetCustomAttribute<ForeignKey<X>>() != null).ToList();
                            if (membersYQuery.Result.Count > 1)
                            {
                                result.Errors.Add(
                                    new DataError(
                                        DataErrorCode.TooMuchMemberFound,
                                        "Too much matching type " + TypeTools.GetReadableName(typeof(X)) + " on type " + TypeTools.GetReadableName(typeof(Y)) + ". Please define a name (" + string.Join(", ", membersYQuery.Result.Select(s => s.Name)) + ")"
                                    )
                                );
                            }
                            else if (membersYQuery.Result.Count == 0)
                            {
                                result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, "The type " + TypeTools.GetReadableName(typeof(X)) + " can't be found on " + TypeTools.GetReadableName(typeof(Y))));
                            }
                            else
                            {
                                reverseMember = membersYQuery.Result[0];
                            }
                        }
                        else
                        {
                            result.Errors.AddRange(membersYQuery.Errors);
                        }
                    }
                    else
                    {
                        reverseMember = membersYQuery.Result[0];
                    }
                }
                else
                {
                    result.Errors.AddRange(membersYQuery.Errors);
                }
            }

            if (reverseMember != null)
            {
                ReverseLinkReverseMember = reverseMember;
                ParameterExpression argParam = Expression.Parameter(typeof(Y), "t");
                Expression nameProperty = Expression.PropertyOrField(argParam, reverseMember.Name);
                Expression body;
                if (reverseMember.IsNullable)
                {
                    List<int?> ids = new List<int?>();
                    Expression<Func<List<int?>>> idLambda = () => ids;
                    body = idLambda.Body;
                }
                else
                {
                    List<int> ids = new List<int>();
                    Expression<Func<List<int>>> idLambda = () => ids;
                    body = idLambda.Body;
                }

                Expression e1 = Expression.Call(body, "Contains", Type.EmptyTypes, nameProperty);
                Expression<Func<Y, bool>> lambda = (Expression<Func<Y, bool>>)Expression.Lambda(e1, argParam);

                IQueryBuilder<Y> query = dmY.CreateQuery<Y>();
                if (fields != null && names.Count == 1)
                {
                    // do it only if its the last elements
                    ParameterExpression argParamReverse = Expression.Parameter(typeof(Y), "t");
                    Expression namePropertyReverse = Expression.PropertyOrField(argParamReverse, reverseMember.Name);
                    LambdaExpression lambdaReverse = Expression.Lambda(namePropertyReverse, argParamReverse);
                    query.Field(lambdaReverse);

                    foreach (Expression<Func<Y, object?>> field in fields)
                    {
                        query.Field(field);
                    }
                }
                if (names.Count > 1)
                {
                    ParameterExpression argParamReverse = Expression.Parameter(typeof(Y), "t");
                    Expression namePropertyReverse = Expression.PropertyOrField(argParamReverse, reverseMember.Name);
                    LambdaExpression lambdaReverse = Expression.Lambda(namePropertyReverse, argParamReverse);
                    query.Field(lambdaReverse);

                    ParameterExpression argParam2 = Expression.Parameter(typeof(Y), "t");
                    Expression nameProperty2 = Expression.PropertyOrField(argParam2, names[1]);
                    for (int i = 2; i < names.Count; i++)
                    {
                        nameProperty2 = Expression.PropertyOrField(nameProperty2, names[i]);
                    }
                    LambdaExpression lambda3 = Expression.Lambda(nameProperty2, argParam2);
                    query.Field(lambda3);
                    query.Include(lambda3, fields);
                }

                ReverseLinkQuery = query.WhereWithParameters(lambda);

            }
        }
        else
        {
            result.Errors.AddRange(memberXQuery.Errors);
        }

        return result;
    }
    public override VoidWithError ExtendReverseLink(List<string> names, List<LambdaExpression>? fields)
    {
        VoidWithError result = new VoidWithError();
        if (ReverseLinkQuery == null || ReverseLinkMemberX == null || ReverseLinkReverseMember == null)
        {
            result.Errors.Add(new DataError(DataErrorCode.ReverseLinkNotPrepared, "The ReverseLink isn't prepared, please open an issue"));
            return result;
        }

        if (names.Count > 1)
        {
            ParameterExpression argParam2 = Expression.Parameter(typeof(Y), "t");
            Expression nameProperty2 = Expression.PropertyOrField(argParam2, names[1]);
            for (int i = 2; i < names.Count; i++)
            {
                nameProperty2 = Expression.PropertyOrField(nameProperty2, names[i]);
            }
            LambdaExpression lambda3 = Expression.Lambda(nameProperty2, argParam2);
            ReverseLinkQuery.Field(lambda3);
            ReverseLinkQuery.Include(lambda3, fields);
        }


        return result;
    }
    #endregion

    #region  External Link
    private QueryBuilderPrepared<Y>? ExternalStorageQuery;
    private DataMemberInfo? ExternalStorageMemberX;
    public async Task<VoidWithError> RunExternalStorage(List<X> items)
    {
        VoidWithError result = new VoidWithError();
        if (ExternalStorageMemberX == null || ExternalStorageQuery == null)
        {
            result.Errors.Add(new DataError(DataErrorCode.ExternalStorageNotPrepared, "The ExternalStorage isn't prepared, please open an issue"));
            return result;
        }

        Dictionary<int, List<IStorable>> elements = new();
        bool isList = false;
        foreach (var item in items)
        {
            object? value = ExternalStorageMemberX.GetValue(item);
            if (value is IStorable storable)
            {
                if (!elements.ContainsKey(storable.Id))
                {
                    elements.Add(storable.Id, new());
                }
                elements[storable.Id].Add(storable);
            }
            else if (value is IList list)
            {
                isList = true;
                foreach (object o in list)
                {
                    if (value is IStorable storable1)
                    {
                        if (!elements.ContainsKey(storable1.Id))
                        {
                            elements.Add(storable1.Id, new());
                        }
                        elements[storable1.Id].Add(storable1);
                    }
                }
            }
        }

        if (elements.Count > 0)
        {
            var query = ExternalStorageQuery.New();
            List<int> ids = elements.Keys.ToList();
            query.Prepare(ids);

            List<Y>? resultTemp = await result.ExtractAsync(query.RunWithError);
            if (resultTemp == null) return result;

            if (isList)
            {
                Dictionary<int, List<Y>> finalResult = new();
                foreach (Y itemTemp in resultTemp)
                {
                    int id = itemTemp.Id;

                    if (elements.ContainsKey(id))
                    {
                        foreach (X element in elements[id])
                        {
                            if (!finalResult.ContainsKey(element.Id))
                            {
                                finalResult[element.Id] = new();
                            }
                            finalResult[element.Id].Add(itemTemp);
                        }
                    }


                }
                foreach (KeyValuePair<int, List<Y>> pair in finalResult)
                {
                    if (elements.ContainsKey(pair.Key))
                    {
                        foreach (X element in elements[pair.Key])
                        {
                            ExternalStorageMemberX.SetValue(element, pair.Value);
                        }
                    }
                }
            }
            else
            {
                foreach (Y itemTemp in resultTemp)
                {
                    int id = itemTemp.Id;

                    if (elements.ContainsKey(id))
                    {
                        foreach (X element in elements[id])
                        {
                            ExternalStorageMemberX.SetValue(element, itemTemp);
                        }
                    }
                }
            }
        }

        return result;
    }
    public override VoidWithError PrepareExternalStorage(List<string> names, List<LambdaExpression>? fields)
    {
        VoidWithError result = new VoidWithError();
        Kind = DatabaseSubBuilderKind.ExternalStorage;

        string name = names[0];
        DataMemberInfo? memberX = result.Extract(() => dmX.GetMemberInfo<X>(name));
        if (memberX == null) return result;
        ExternalStorageMemberX = memberX;

        ReverseLink? reverseLinkAttr = memberX.GetCustomAttribute<ReverseLink>();
        if (reverseLinkAttr != null)
        {
            result.Errors.Add(new DataError(DataErrorCode.ReverseLinkExist, "The field " + memberX.Name + " is a ReverseLink"));
            return result;
        }

        ParameterExpression argParam = Expression.Parameter(typeof(Y), "t");
        Expression nameProperty = Expression.PropertyOrField(argParam, Storable.Id);
        Expression body;
        List<int> ids = new List<int>();
        Expression<Func<List<int>>> idLambda = () => ids;
        body = idLambda.Body;
        Expression e1 = Expression.Call(body, "Contains", Type.EmptyTypes, nameProperty);
        Expression<Func<Y, bool>> lambda = (Expression<Func<Y, bool>>)Expression.Lambda(e1, argParam);

        IQueryBuilder<Y> query = dmY.CreateQuery<Y>();
        if (fields != null && names.Count == 1)
        {
            ParameterExpression argParam2 = Expression.Parameter(typeof(Y), "t");
            Expression nameProperty2 = Expression.PropertyOrField(argParam2, Storable.Id);
            LambdaExpression lambda2 = Expression.Lambda(nameProperty2, argParam2);
            query.Field(lambda2);

            foreach (Expression<Func<Y, object?>> field in fields)
            {
                query.Field(field);
            }
        }
        if (names.Count > 1)
        {
            ParameterExpression argParam2 = Expression.Parameter(typeof(Y), "t");
            Expression nameProperty2 = Expression.PropertyOrField(argParam2, names[1]);
            for (int i = 2; i < names.Count; i++)
            {
                nameProperty2 = Expression.PropertyOrField(nameProperty2, names[i]);
            }
            LambdaExpression lambda3 = Expression.Lambda(nameProperty2, argParam2);
            query.Field(lambda3);
            query.Include(lambda3, fields);
        }


        ExternalStorageQuery = query.WhereWithParameters(lambda);
        return result;
    }
    public override VoidWithError ExtendExternalStorage(List<string> names, List<LambdaExpression>? fields)
    {
        VoidWithError result = new VoidWithError();
        if (ExternalStorageMemberX == null || ExternalStorageQuery == null)
        {
            result.Errors.Add(new DataError(DataErrorCode.ExternalStorageNotPrepared, "The ExternalStorage isn't prepared, please open an issue"));
            return result;
        }

        if (names.Count > 1)
        {
            ParameterExpression argParam2 = Expression.Parameter(typeof(Y), "t");
            Expression nameProperty2 = Expression.PropertyOrField(argParam2, names[1]);
            for (int i = 2; i < names.Count; i++)
            {
                nameProperty2 = Expression.PropertyOrField(nameProperty2, names[i]);
            }
            LambdaExpression lambda3 = Expression.Lambda(nameProperty2, argParam2);
            ExternalStorageQuery.Field(lambda3);
            ExternalStorageQuery.Include(lambda3, fields);
        }


        return result;
    }


    #endregion

}