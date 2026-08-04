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
        private readonly bool NeedUpdateField;
        public bool AllFieldsUpdate { get; private set; } = true;

        public DatabaseUpdateBuilder(IDBStorage storage, IGenericDM dm, bool needUpdateField, Type? baseType = null) : base(storage, dm, baseType)
        {
            NeedUpdateField = needUpdateField;
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
            MergeScopeAndWhere();
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
                    result.Errors.Add(new DataError(DataErrorCode.NumberOfItemsNotMatching, "Can't update single because the action return " + resultTemp.Result.Count + " item"));
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
            WhereGeneric(func);
            return this;
        }

        public IUpdateBuilder<T> OrWhere(Expression<Func<T, bool>> func)
        {
            OrWhereGeneric(func);
            return this;
        }

        public UpdateBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func)
        {
            WhereGenericWithParameters(func);
            return new(this);
        }

        void IUpdateBuilder<T>.PrepareInternal(params object[] objects)
        {
            PrepareGeneric(objects);
        }

        void IUpdateBuilder<T>.SetVariableInternal(string name, object value)
        {
            SetVariableGeneric(name, value);
        }
        void IUpdateBuilder<T>.ResetPreparedParametersInternal()
        {
            ResetPreparedParametersGeneric();
        }

        public IUpdateBuilder<T> WithScope<X>() where X : IScope, new()
        {
            WithScopeGeneric<X>();
            return this;
        }
        public IUpdateBuilder<T> WithoutScope()
        {
            WithoutScopeGeneric();
            return this;
        }
    }
}
