using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Tools;

namespace AventusSharp.Data.Manager;


public class LoaderHelper
{

    #region Load object from Id
    public static async Task<ResultWithError<List<Y>>> LoadObjectFromId<X, Y>(ResultWithError<List<X>> from, Func<X, int> fct, Action<X, Y> set) where X : IStorable where Y : IStorable
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
                result = await GenericDM.Get<Y>().WhereWithError<Y>(p => ids.Contains(p.Id));
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

    public static async Task<ResultWithError<List<Y>>> LoadObjectsFromIds<X, Y>(ResultWithError<List<X>> from, Func<X, List<int>> fct, Action<X, Y> set) where X : IStorable where Y : IStorable
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
                result = await GenericDM.Get<Y>().WhereWithError<Y>(p => ids.Contains(p.Id));
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

    #endregion

    #region Load
    internal static async Task<VoidWithError> LoadInternal<X>(ResultWithError<List<X>> from, List<Expression<Func<X, object?>>> expressions) where X : IStorable
    {
        VoidWithError result = new VoidWithError();
        if (!from.Success || from.Result == null)
        {
            result.Errors = from.Errors;
            return result;
        }
        if (expressions.Count == 0) return result;

        try
        {
            IGenericDM dmX = GenericDM.Get<X>();
            Dictionary<int, X> elements = from.Result.ToDictionary(p => p.Id, p => p);
            List<int> ids = elements.Keys.ToList();

            if (ids.Count > 0)
            {
                var query = dmX.CreateQuery<X>();
                query.Field(p => p.Id);

                List<List<DataMemberInfo>> fields = new();
                foreach (var exp in expressions)
                {
                    fields.Add(LambdaTranslator.ExtractMembers(exp));
                    query.Field(exp);
                }
                query.Where(p => ids.Contains(p.Id));
                List<X>? resultTemp = await result.ExtractAsync(query.RunWithError);
                if (resultTemp == null) return result;

                foreach (X itemTemp in resultTemp)
                {
                    if (!elements.ContainsKey(itemTemp.Id)) continue;
                    X realItem = elements[itemTemp.Id];

                    foreach (List<DataMemberInfo> fieldStep in fields)
                    {
                        object? o1 = realItem;
                        object? o2 = itemTemp;
                        for (int i = 0; i < fieldStep.Count; i++)
                        {
                            DataMemberInfo field = fieldStep[i];
                            o2 = field.GetValue(o2);
                            if (o2 == null) break;

                            object? o1Temp = field.GetValue(o1);
                            if (o1Temp == null || i == fieldStep.Count - 1)
                            {
                                field.SetValue(o1, o2);
                                break;
                            }
                            o1 = o1Temp;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
        }

        return result;
    }

    #endregion

}