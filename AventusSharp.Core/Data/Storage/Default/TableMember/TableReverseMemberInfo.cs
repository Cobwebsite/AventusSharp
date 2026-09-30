using AventusSharp.Localization;
using AventusSharp.Data.Attributes;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Data;
using AventusSharp.Data.Manager;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AventusSharp.Data.Storage.Default.TableMember
{
    public class TableReverseMemberInfo : TableMemberInfo
    {
        public TableReverseMemberInfo(TableInfo tableInfo) : base(tableInfo)
        {
        }

        public TableReverseMemberInfo(FieldInfo fieldInfo, TableInfo tableInfo) : base(fieldInfo, tableInfo)
        {
        }

        public TableReverseMemberInfo(PropertyInfo propertyInfo, TableInfo tableInfo) : base(propertyInfo, tableInfo)
        {
        }

        public TableReverseMemberInfo(MemberInfo? memberInfo, TableInfo tableInfo) : base(memberInfo, tableInfo)
        {
        }


        public Func<List<int>, Task<ResultWithDataError<List<IStorable>>>>? reverseQueryBuilder;
        public TableMemberInfoSql? reverseMember;
        public Type? ReverseLinkType;
        public bool isSingle = false;
        private ReverseLink? ReverseLinkAttr;
        public TableInfo? TableLinked;
        public VoidWithDataError Prepare()
        {
            VoidWithDataError result = new();
            if (memberInfo != null)
            {
                Type? type = TableMemberInfoSql.IsListTypeUsable(MemberType);
                if (type == null)
                {
                    type = TableMemberInfoSql.IsDictionaryTypeUsable(MemberType);
                    if (type == null)
                    {
                        type = MemberType;
                        isSingle = true;
                    }
                }
                ReverseLinkType = type;
            }
            else
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, AventusTranslations.Get(AventusMessageKeys.Data.UnexpectedCase)));
            }
            return result;
        }
        public VoidWithDataError PrepareReverseLink(TableInfo tableInfo)
        {
            VoidWithDataError result = new();
            TableLinked = tableInfo;

            if (ReverseLinkAttr?.field != null)
            {
                TableMemberInfoSql? reversInfo = null;
                TableInfo? el = tableInfo;
                while (el != null)
                {
                    reversInfo = tableInfo.Members.Find(m => m.Name == ReverseLinkAttr.field);
                    if (reversInfo == null)
                    {
                        el = el.Parent;
                        continue;
                    }
                    break;
                }
                if (reversInfo == null)
                {
                    result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, AventusTranslations.Get(AventusMessageKeys.Data.NameNotFound, ReverseLinkAttr.field, tableInfo.Name)));
                }
                else
                {
                    reverseMember = reversInfo;
                }
            }
            else
            {
                List<TableMemberInfoSql> reversInfo = new List<TableMemberInfoSql>();
                TableInfo? el = tableInfo;
                while (el != null)
                {
                    reversInfo.AddRange(tableInfo.Members.Where(m => m is ITableMemberInfoSqlLink link && link.TableLinkedType == TableInfo.Type).ToList());
                    el = el.Parent;
                }
                if (reversInfo.Count > 1)
                {
                    result.Errors.Add(
                        new DataError(
                            DataErrorCode.TooMuchMemberFound,
                            AventusTranslations.Get(AventusMessageKeys.Data.AmbiguousMember, TableInfo.Type, tableInfo.Name, string.Join(", ", reversInfo.Select(s => s.Name)))
                        )
                    );
                }
                else if (reversInfo.Count == 0)
                {
                    result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, AventusTranslations.Get(AventusMessageKeys.Data.MemberTypeNotFound, TableInfo.Type, tableInfo.Name)));
                }
                else
                {
                    reverseMember = reversInfo[0];
                }
            }
            return result;
        }

        public Task<ResultWithDataError<List<IStorable>>> ReverseQuery(int id)
        {
            return ReverseQuery(new List<int> { id });
        }

        public async Task<ResultWithDataError<List<IStorable>>> ReverseQuery(List<int> ids)
        {
            ResultWithDataError<List<IStorable>> result = new();
            try
            {
                if (ids.Count == 0)
                {
                    result.Result = new List<IStorable>();
                    return result;
                }

                if (reverseQueryBuilder == null)
                {
                    if (ReverseLinkType == null || reverseMember == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.ReverseLinkNotExist, AventusTranslations.Get(AventusMessageKeys.Data.ReverseLinkNotInitialized, Name)));
                        return result;
                    }

                    ParameterExpression argParam = Expression.Parameter(ReverseLinkType, "t");
                    Expression nameProperty;
                    if (TypeTools.IsPrimitiveType(reverseMember.MemberType))
                    {
                        nameProperty = Expression.PropertyOrField(argParam, reverseMember.Name);
                    }
                    else
                    {
                        Expression temp = Expression.PropertyOrField(argParam, reverseMember.Name);
                        nameProperty = Expression.PropertyOrField(temp, Storable.Id);
                    }
                    List<int> queryIds = new();
                    Expression<Func<List<int>>> idsLambda = () => queryIds;

                    Type? typeIfNullable = System.Nullable.GetUnderlyingType(nameProperty.Type);
                    if (typeIfNullable != null)
                    {
                        nameProperty = Expression.Call(nameProperty, "GetValueOrDefault", Type.EmptyTypes);
                    }

                    Expression e1 = Expression.Call(
                        idsLambda.Body,
                        typeof(List<int>).GetMethod(nameof(List<>.Contains), [typeof(int)])!,
                        nameProperty
                    );
                    LambdaExpression lambda = Expression.Lambda(e1, argParam);

                    IGenericDM dm = GenericDM.Get(ReverseLinkType);
                    Type t = reverseMember.TableInfo.Type.ContainsGenericParameters ? dm.GetMainType() : reverseMember.TableInfo.Type;
                    object? query = dm.GetType().GetMethod("CreateQuery")?.MakeGenericMethod(t).Invoke(dm, null);
                    if (query == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.ErrorCreatingReverseQuery, AventusTranslations.Get(AventusMessageKeys.Data.QueryCreationFailed)));
                        return result;
                    }
                    MethodInfo? whereWithParam = query.GetType().GetMethod("WhereWithParameters");
                    if (whereWithParam == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.ErrorCreatingReverseQuery, AventusTranslations.Get(AventusMessageKeys.Data.WhereWithParamMissing)));
                        return result;
                    }

                    var prepared = whereWithParam.Invoke(query, new object[] { lambda });
                    IQueryBuilderPrepared preparedQuery;
                    if (prepared is IQueryBuilderPrepared _preparedQuery)
                    {
                        preparedQuery = _preparedQuery;
                    }
                    else
                    {
                        result.Errors.Add(new DataError(DataErrorCode.ErrorCreatingReverseQuery, AventusTranslations.Get(AventusMessageKeys.Data.RunWithErrorMissing)));
                        return result;
                    }

                    reverseQueryBuilder = async delegate (List<int> queryIds)
                    {
                        ResultWithDataError<List<IStorable>> result = new();
                        IResultWithError? resultWithError = await _preparedQuery.New().SetVariables((define) =>
                        {
                            define(nameof(queryIds), queryIds);
                        }).RunWithError();
                        if (resultWithError != null)
                        {
                            foreach (GenericError error in resultWithError.Errors)
                            {
                                if (error is DataError dataError)
                                {
                                    result.Errors.Add(dataError);
                                }
                            }
                            result.Result = new List<IStorable>();
                            if (resultWithError.Result is IList list)
                            {
                                foreach (object item in list)
                                {
                                    if (item is IStorable storable)
                                    {
                                        result.Result.Add(storable);
                                    }
                                }
                            }
                        }
                        return result;
                    };
                }
                result = await reverseQueryBuilder(ids.Distinct().ToList());
                if (result.Result != null)
                {
                    result.Result = result.Result.GroupBy(item => item.Id).Select(group => group.Last()).ToList();
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
            }
            return result;
        }

        public void SetReverseId(object o, int id)
        {
            if (reverseMember == null)
            {
                return;
            }

            // check if int id or object
            if (TableMemberInfoSql.IsTypeUsable(reverseMember.MemberType))
            {
                IStorable el = TypeTools.CreateNewObj<IStorable>(reverseMember.MemberType);
                el.Id = id;
                reverseMember.SetValue(o, el);
            }
            else
            {
                reverseMember.SetValue(o, id);
            }
        }


        protected override void ParseAttributes()
        {
            IsAutoRead = false;
            IsAutoCreate = false;
            IsAutoDelete = false;
            IsAutoUpdate = false;
            base.ParseAttributes();
        }
        protected override bool ParseAttribute(Attribute attribute)
        {
            if (base.ParseAttribute(attribute))
            {
                return true;
            }

            if (attribute is ReverseLink reverseLinkAttr)
            {
                ReverseLinkAttr = reverseLinkAttr;
                return true;
            }
            return false;
        }
    }
}
