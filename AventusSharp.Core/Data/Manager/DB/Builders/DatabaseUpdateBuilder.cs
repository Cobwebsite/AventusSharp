using AventusSharp.Localization;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager.DB.Builders
{
    public class DatabaseUpdateBuilderInfo
    {
        public string QuerySql { get; set; }
        public List<DatabaseUpdateBuilderInfoQuery> Queries { get; set; } = new List<DatabaseUpdateBuilderInfoQuery>();

        public List<TableReverseMemberInfo> ReverseMembers { get; set; } = new();

        public List<TableMemberInfoSql> ToCheckBefore { get; set; } = new();

        public DatabaseUpdateBuilderInfo(string querySql)
        {
            QuerySql = querySql;
        }
    }
    public class DatabaseUpdateBuilderInfoQuery
    {
        public string Sql { get; set; }
        public List<ParamsInfo> Parameters { get; }
        public List<ParamsInfo> ParametersGrap { get; }


        public DatabaseUpdateBuilderInfoQuery(string sql, List<ParamsInfo> parameters, List<ParamsInfo> parametersGrap)
        {
            Sql = sql;
            Parameters = parameters;
            ParametersGrap = parametersGrap;
        }

    }
    //public class DatabaseUpdateBuilderInfo
    //{
    //    public string UpdateSql { get; set; }
    //    public string QuerySql { get; set; }

    //    public List<TableReverseMemberInfo> ReverseMembers { get; set; } = new();

    //    public List<TableMemberInfoSql> ToCheckBefore { get; set; } = new();

    //    public DatabaseUpdateBuilderInfo(string updateSql, string querySql)
    //    {
    //        UpdateSql = updateSql;
    //        QuerySql = querySql;
    //    }
    //}
    public class DatabaseUpdateBuilder<T> : DatabaseGenericBuilder<T>, ILambdaTranslatable, IUpdateBuilder<T> where T : IStorable
    {
        public Dictionary<string, ParamsInfo> UpdateParamsInfo { get; set; } = new Dictionary<string, ParamsInfo>();

        public DatabaseUpdateBuilderInfo? Query { get; set; }
        private DatabaseQueryBuilder<T>? selectionQuery;
        private readonly List<(Expression<Func<T, bool>> Predicate, bool IsOr)> selectionPredicates = new();
        private readonly bool NeedUpdateField;
        public bool AllFieldsUpdate { get; private set; } = true;

        public DatabaseUpdateBuilder(IDBStorage storage, IGenericDM dm, bool needUpdateField, Type? baseType = null) : base(storage, dm, baseType)
        {
            NeedUpdateField = needUpdateField;
        }

        private DatabaseQueryBuilder<T> GetSelectionQuery()
        {
            if (selectionQuery != null)
            {
                return selectionQuery;
            }

            DatabaseQueryBuilder<T> query = new DatabaseQueryBuilder<T>(Storage, DM);
            if (_noScope)
            {
                query.WithoutScope();
            }
            else if (ManualScopes != null)
            {
                foreach (IScope scope in ManualScopes)
                {
                    query.WithScope(scope);
                }
            }
            foreach ((Expression<Func<T, bool>> Predicate, bool IsOr) predicate in selectionPredicates)
            {
                if (predicate.IsOr)
                {
                    query.OrWhere(predicate.Predicate);
                }
                else
                {
                    query.Where(predicate.Predicate);
                }
            }
            selectionQuery = query;
            return query;
        }


        public async Task<List<T>?> Run(T item)
        {
            ResultWithError<List<T>> result = await RunWithError(item);
            if (result.Success && result.Result != null)
            {
                return result.Result;
            }
            return null;
        }

        public async Task<ResultWithError<List<T>>> RunWithError(T item)
        {
            ResultWithError<List<T>> result = new();
            List<GenericError> runErrors = GetRunErrors();
            if (runErrors.Count > 0)
            {
                result.Errors = runErrors;
                return result;
            }
            if (selectionQuery?.RequiresPostProcessing == true)
            {
                List<T>? selected = await result.ExtractAsync(selectionQuery.RunWithError);
                if (selected == null)
                    return result;

                List<int> ids = selected.Select(value => value.Id).Distinct().ToList();
                if (ids.Count == 0)
                {
                    result.Result = new List<T>();
                    return result;
                }
                Expression<Func<T, bool>> idPredicate = value => ids.Contains(value.Id);
                Wheres = new LambdaTranslator<T>(this).Translate(idPredicate).Wheres;
                Query = null;
            }
            else
            {
                MergeScopeAndWhere();
            }
            ResultWithError<List<int>> resultTemp = await Storage.UpdateFromBuilder(this, item);
            if (resultTemp.Success && resultTemp.Result != null)
            {
                ResultWithError<List<T>> resultQuery = await DM.GetByIdsWithError<T>(resultTemp.Result);
                if (resultQuery.Success && resultQuery.Result != null)
                {
                    // update data in cache
                    if (NeedUpdateField)
                    {
                        foreach (KeyValuePair<string, ParamsInfo> paramUpdated in UpdateParamsInfo)
                        {
                            foreach (T resultItem in resultQuery.Result)
                            {
                                paramUpdated.Value.SetCurrentValueOnObject(resultItem);
                            }
                        }
                    }
                    result.Result = resultQuery.Result;
                }
                else
                {
                    result.Errors.AddRange(resultQuery.Errors);
                }
            }
            else
            {
                result.Errors.AddRange(resultTemp.Errors);
            }
            DM.PrintErrors(result);
            return result;
        }

        public async Task<T?> Single(T item)
        {
            return (await SingleWithError(item)).Result;
        }
        public async Task<ResultWithError<T>> SingleWithError(T item)
        {
            ResultWithError<T> result = new();
            ResultWithError<List<T>> resultTemp = await RunWithError(item);

            if (resultTemp.Success && resultTemp.Result != null)
            {
                if (resultTemp.Result.Count <= 1)
                {
                    foreach (KeyValuePair<string, ParamsInfo> paramUpdated in UpdateParamsInfo)
                    {
                        paramUpdated.Value.SetCurrentValueOnObject(item);
                    }
                    result.Result = item;
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.NumberOfItemsNotMatching, AventusTranslations.Get(AventusMessageKeys.Data.UpdateSingleCountMismatch, resultTemp.Result.Count)));
                }
            }
            else
                result.Errors.AddRange(resultTemp.Errors);

            DM.PrintErrors(result);
            return result;

        }


        public IUpdateBuilder<T> Field<U>(Expression<Func<T, U>> fct)
        {
            AllFieldsUpdate = false;
            string fieldPath = FieldGeneric(fct);
            // string[] splitted = fieldPath.Split(".");
            // string current = "";
            // List<TableMemberInfoSql> access = new();
            // string lastAlias = "";
            // foreach (string s in splitted)
            // {
            //     if (InfoByPath[current] != null)
            //     {
            //         KeyValuePair<TableMemberInfoSql?, string> infoTemp = InfoByPath[current].GetTableMemberInfoAndAlias(s);
            //         if (infoTemp.Key == null)
            //         {
            //             throw new Exception("Can't find the field " + s + " on the path " + current);
            //         }
            //         access.Add(infoTemp.Key);
            //         lastAlias = infoTemp.Value;
            //         if (current != "")
            //         {
            //             current += "." + s;
            //         }
            //         else
            //         {
            //             current += s;
            //         }
            //     }
            // }

            // TableMemberInfoSql lastMemberInfo = access.Last();
            // if (lastMemberInfo is ITableMemberInfoSqlWritable writable)
            // {
            //     string name = lastAlias + "." + lastMemberInfo.SqlName;

            //     UpdateParamsInfo[name] = new ParamsInfo()
            //     {
            //         DbType = writable.SqlType,
            //         Name = name,
            //         MembersList = access,
            //     };
            // }
            return this;
        }

        public IUpdateBuilder<T> Where(Expression<Func<T, bool>> func)
        {
            bool external = ExternalExpressionLoader<T>.RequiresExternal(this, func);
            if (selectionQuery == null && !external)
            {
                WhereGeneric(func);
                selectionPredicates.Add((func, false));
                return this;
            }

            GetSelectionQuery().Where(func);
            selectionPredicates.Add((func, false));
            return this;
        }

        public IUpdateBuilder<T> OrWhere(Expression<Func<T, bool>> func)
        {
            bool external = ExternalExpressionLoader<T>.RequiresExternal(this, func);
            if (selectionQuery == null && !external)
            {
                OrWhereGeneric(func);
                selectionPredicates.Add((func, true));
                return this;
            }

            GetSelectionQuery().OrWhere(func);
            selectionPredicates.Add((func, true));
            return this;
        }

        public UpdateBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func)
        {
            if (selectionPredicates.Count > 0)
            {
                WhereGenericWithParameters(func);
                return new(this);
            }

            if (!ExternalExpressionLoader<T>.RequiresExternal(this, func))
            {
                WhereGenericWithParameters(func);
            }
            else
            {
                GetSelectionQuery().WhereWithParameters(func);
            }
            return new(this);
        }

        void IUpdateBuilder<T>.PrepareInternal(params object[] objects)
        {
            PrepareGeneric(objects);
            ((IQueryBuilder<T>?)selectionQuery)?.PrepareInternal(objects);
        }

        void IUpdateBuilder<T>.SetVariableInternal(string name, object value)
        {
            SetVariableGeneric(name, value);
            ((IQueryBuilder<T>?)selectionQuery)?.SetVariableInternal(name, value);
        }
        void IUpdateBuilder<T>.ResetPreparedParametersInternal()
        {
            ResetPreparedParametersGeneric();
            ((IQueryBuilder<T>?)selectionQuery)?.ResetPreparedParametersInternal();
        }

        public IUpdateBuilder<T> WithScope<X>() where X : IScope, new()
        {
            WithScopeGeneric<X>();
            selectionQuery?.WithScope(ManualScopes!.Last());
            return this;
        }
        public IUpdateBuilder<T> WithoutScope()
        {
            WithoutScopeGeneric();
            selectionQuery?.WithoutScope();
            return this;
        }
    }
}
