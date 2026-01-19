using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Tools;

namespace AventusSharp.Data.Manager;


public class LoaderHelper
{
    public static ResultWithError<List<Y>> LoadDependances<X, Y>(ResultWithError<List<X>> from, Func<X, int> fct, Action<X, Y> set) where X : IStorable where Y : IStorable
    {
        ResultWithError<List<Y>> result = new ResultWithError<List<Y>>();
        if (!from.Success || from.Result == null)
        {
            result.Errors = from.Errors;
        }
        else
        {
            List<int> ids = new List<int>();
            foreach (X recolte in from.Result)
            {
                int id = fct(recolte);
                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            if (ids.Count > 0)
            {
                result = GenericDM.Get<Y>().WhereWithError<Y>(p => ids.Contains(p.Id));
                if (result.Success && result.Result != null)
                {
                    Dictionary<int, Y> dico = result.Result.ToDictionary(p => p.Id, p => p);
                    foreach (X recolte in from.Result)
                    {
                        int id = fct(recolte);
                        if (dico.ContainsKey(id))
                        {
                            set(recolte, dico[id]);
                        }
                        else
                        {
                            // result.Errors.Add(new )
                        }
                    }
                }
            }

        }

        return result;
    }

    public static ResultWithError<List<Y>> LoadDependancesList<X, Y>(ResultWithError<List<X>> from, Func<X, List<int>> fct, Action<X, Y> set) where X : IStorable where Y : IStorable
    {
        ResultWithError<List<Y>> result = new ResultWithError<List<Y>>();
        if (!from.Success || from.Result == null)
        {
            result.Errors = from.Errors;
        }
        else
        {
            List<int> ids = new List<int>();
            foreach (X recolte in from.Result)
            {
                List<int> idTemps = fct(recolte);
                foreach (int id in idTemps)
                {
                    if (!ids.Contains(id))
                    {
                        ids.Add(id);
                    }
                }
            }

            if (ids.Count > 0)
            {
                result = GenericDM.Get<Y>().WhereWithError<Y>(p => ids.Contains(p.Id));
                if (result.Success && result.Result != null)
                {
                    Dictionary<int, Y> dico = result.Result.ToDictionary(p => p.Id, p => p);
                    foreach (X recolte in from.Result)
                    {
                        List<int> idTemps = fct(recolte);
                        foreach (int id in idTemps)
                        {
                            if (dico.ContainsKey(id))
                            {
                                set(recolte, dico[id]);
                            }
                            else
                            {
                                // result.Errors.Add(new )
                            }
                        }
                    }
                }
            }

        }

        return result;
    }

    public static VoidWithError LoadReverseLink<X, Y>(ResultWithError<List<X>> from, Expression<Func<X, List<Y>>> expression) where X : IStorable where Y : IStorable
    {
        string name = LambdaTranslator.ExtractName(expression);
        return LoadReverseLinkInternal<X, Y>(from, name);
    }
    public static VoidWithError LoadReverseLink<X, Y>(List<X> from, Expression<Func<X, List<Y>>> expression) where X : IStorable where Y : IStorable
    {
        string name = LambdaTranslator.ExtractName(expression);
        return LoadReverseLinkInternalList<X, Y>(from, name);
    }
    internal static VoidWithError LoadReverseLinkInternalList<X, Y>(List<X> from, string name) where X : IStorable where Y : IStorable
    {
        return LoadReverseLinkInternal<X, Y>(new ResultWithError<List<X>>() { Result = from }, name);
    }
    internal static VoidWithError LoadReverseLinkInternal<X, Y>(ResultWithError<List<X>> from, string name) where X : IStorable where Y : IStorable
    {
        VoidWithError result = new VoidWithError();
        if (!from.Success || from.Result == null)
        {
            result.Errors = from.Errors;
            return result;
        }
        List<int> ids = new List<int>();
        Dictionary<int, List<X>> elements = new();
        foreach (X item in from.Result)
        {
            if (!ids.Contains(item.Id))
            {
                ids.Add(item.Id);
                elements[item.Id] = new();
            }
            elements[item.Id].Add(item);
        }

        if (ids.Count > 0)
        {
            IGenericDM dmX = GenericDM.Get<X>();
            IGenericDM dmY = GenericDM.Get<Y>();
            ResultWithError<DataMemberInfo> memberXQuery = dmX.GetMemberInfo<X>(name);
            if (memberXQuery.Result != null && memberXQuery.Success)
            {
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
                    ParameterExpression argParam = Expression.Parameter(typeof(Y), "t");
                    Expression nameProperty = Expression.PropertyOrField(argParam, reverseMember.Name);
                    Expression body;
                    if (reverseMember.IsNullable)
                    {
                        List<int?> idsNull = ids.Select(p => (int?)p).ToList();
                        Expression<Func<List<int?>>> idLambda = () => idsNull;
                        body = idLambda.Body;
                    }
                    else
                    {
                        Expression<Func<List<int>>> idLambda = () => ids;
                        body = idLambda.Body;
                    }

                    Expression e1 = Expression.Call(body, "Contains", Type.EmptyTypes, nameProperty);
                    Expression<Func<Y, bool>> lambda = (Expression<Func<Y, bool>>)Expression.Lambda(e1, argParam);

                    ResultWithError<List<Y>> linkedElement = dmY.WhereWithError(lambda);
                    if (linkedElement.Success && linkedElement.Result != null)
                    {
                        foreach (Y item in linkedElement.Result)
                        {
                            object? reverseItem = reverseMember.GetValue(item);
                            List<X> elementList = new();
                            if (reverseItem is int reverseId)
                            {
                                elementList = elements[reverseId];
                            }
                            else if (reverseItem is IStorable reverseItem2)
                            {
                                elementList = elements[reverseItem2.Id];
                            }
                            foreach (X element in elementList)
                            {
                                object? list = memberXQuery.Result.GetValue(element);
                                if (list is null)
                                {
                                    bool isList = memberXQuery.Result.Type?.GetInterfaces().Contains(typeof(IList)) ?? false;
                                    if (isList)
                                    {
                                        list = Activator.CreateInstance(memberXQuery.Result.Type!);
                                        memberXQuery.Result.SetValue(element, list);
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
                                        memberXQuery.Result.SetValue(element, item);
                                    }
                                    catch (Exception e)
                                    {
                                        result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        result.Errors.AddRange(linkedElement.Errors);
                    }
                }
            }
            else
            {
                result.Errors.AddRange(memberXQuery.Errors);
            }
        }

        return result;
    }

}