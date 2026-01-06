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


        public Func<int, ResultWithDataError<List<IStorable>>>? reverseQueryBuilder;
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
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, "Impossible case"));
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
                        el = tableInfo.Parent; continue;
                    }
                    break;
                }
                if (reversInfo == null)
                {
                    result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, "The name " + ReverseLinkAttr.field + " can't be found on " + tableInfo.Name));
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
                    el = tableInfo.Parent;
                }
                if (reversInfo.Count > 1)
                {
                    result.Errors.Add(
                        new DataError(
                            DataErrorCode.TooMuchMemberFound,
                            "Too much matching type " + TableInfo.Type + " on type " + tableInfo.Name + ". Please define a name (" + string.Join(", ", reversInfo.Select(s => s.Name)) + ")"
                        )
                    );
                }
                else if (reversInfo.Count == 0)
                {
                    result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, "The type " + TableInfo.Type + " can't be found on " + tableInfo.Name));
                }
                else
                {
                    reverseMember = reversInfo[0];
                }
            }
            return result;
        }

        public VoidWithDataError ReverseLoadAndSet(IStorable o)
        {
            VoidWithDataError result = new();
            if (TableLinked == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.LinkNotSet, "The table linked isn't set => internal error : contact an admin"));
                return result;
            }
            object? iresultTemp = GetType().GetMethod("_ReverseQuery", BindingFlags.NonPublic | BindingFlags.Instance)?.MakeGenericMethod(TableLinked.Type).Invoke(this, new object[] { o.Id });
            if (iresultTemp is IResultWithError resultTemp)
            {
                if (resultTemp.Errors.Count > 0)
                {
                    foreach (var errorTemp in resultTemp.Errors)
                    {
                        if (errorTemp is DataError dataError)
                        {
                            result.Errors.Add(dataError);
                        }
                    }
                    return result;
                }


                if (isSingle)
                {
                    if (resultTemp.Result is IList list && list.Count > 0)
                    {
                        SetValue(o, list[0]);
                    }
                    else
                    {
                        SetValue(o, null);
                    }
                }
                else
                {
                    SetValue(o, resultTemp.Result);
                }
            }
            return result;
        }
        public ResultWithDataError<List<IStorable>> ReverseQuery(int Id)
        {
            ResultWithDataError<List<IStorable>> result = new();
            if (reverseQueryBuilder == null)
            {
                if (ReverseLinkType == null || reverseMember == null)
                {
                    result.Errors.Add(new DataError(DataErrorCode.ReverseLinkNotExist, "Reverse link seems to not be init : " + Name));
                    return result;
                }

                ParameterExpression argParam = Expression.Parameter(ReverseLinkType, "t");
                Expression nameProperty;
                Type varType;
                if (TypeTools.IsPrimitiveType(reverseMember.MemberType))
                {
                    varType = reverseMember.MemberType;
                    nameProperty = Expression.PropertyOrField(argParam, reverseMember.SqlName);
                }
                else
                {
                    varType = typeof(int);
                    Expression temp = Expression.PropertyOrField(argParam, reverseMember.SqlName);
                    nameProperty = Expression.PropertyOrField(temp, Storable.Id);
                }
                Expression<Func<int>> idLambda = () => Id;

                Type? typeIfNullable = System.Nullable.GetUnderlyingType(nameProperty.Type);
                if (typeIfNullable != null)
                {
                    nameProperty = Expression.Call(nameProperty, "GetValueOrDefault", Type.EmptyTypes);
                }

                Expression e1 = Expression.Equal(nameProperty, idLambda.Body);
                LambdaExpression lambda = Expression.Lambda(e1, argParam);

                IGenericDM dm = GenericDM.Get(ReverseLinkType);
                object? query = dm.GetType().GetMethod("CreateQuery")?.MakeGenericMethod(reverseMember.TableInfo.Type).Invoke(dm, null);
                if (query == null)
                {
                    result.Errors.Add(new DataError(DataErrorCode.ErrorCreatingReverseQuery, "Can't create the query"));
                    return result;
                }
                // MethodInfo? setVariable = query.GetType().GetMethod("SetVariable");
                // if (setVariable == null)
                // {
                //     result.Errors.Add(new DataError(DataErrorCode.ErrorCreatingReverseQuery, "Can't get the function setVariable"));
                //     return result;
                // }
                // MethodInfo? runWithError = query.GetType().GetMethod("RunWithError");
                // if (runWithError == null)
                // {
                //     result.Errors.Add(new DataError(DataErrorCode.ErrorCreatingReverseQuery, "Can't get the function runWithError"));
                //     return result;
                // }
                MethodInfo? whereWithParam = query.GetType().GetMethod("WhereWithParameters");
                if (whereWithParam == null)
                {
                    result.Errors.Add(new DataError(DataErrorCode.ErrorCreatingReverseQuery, "Can't get the function whereWithParam"));
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
                    result.Errors.Add(new DataError(DataErrorCode.ErrorCreatingReverseQuery, "Can't get the function runWithError"));
                    return result;
                }

                reverseQueryBuilder = delegate (int id)
                {
                    ResultWithDataError<List<IStorable>> result = new();
                    IResultWithError? resultWithError = _preparedQuery.New().SetVariables((define) =>
                    {
                        define(Storable.Id, id);
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
            result = reverseQueryBuilder(Id);
            return result;
        }

        public ResultWithDataError<List<IStorable>> ReverseQuery(List<int> ids)
        {
            // TODO change the list to be the main code used by single id
            ResultWithDataError<List<IStorable>> result = new();
            Dictionary<int, IStorable> elements = new Dictionary<int, IStorable>();
            foreach (int id in ids)
            {
                ResultWithDataError<List<IStorable>> resultTemp = ReverseQuery(id);
                if (!resultTemp.Success)
                {
                    result.Errors.AddRange(resultTemp.Errors);
                    return result;
                }
                if (resultTemp.Result == null)
                {
                    continue;
                }
                foreach (IStorable storable in resultTemp.Result)
                {
                    elements[storable.Id] = storable;
                }
            }
            result.Result = elements.Select(p => p.Value).ToList();
            return result;
        }

        private ResultWithDataError<List<X>> _ReverseQuery<X>(int id) where X : IStorable
        {
            ResultWithDataError<List<X>> result = new ResultWithDataError<List<X>>();
            ResultWithDataError<List<IStorable>> resultTemp = ReverseQuery(id);
            result.Result = new List<X>();
            if (!resultTemp.Success || resultTemp.Result == null)
            {
                result.Errors.AddRange(resultTemp.Errors);
                return result;
            }

            foreach (IStorable storable in resultTemp.Result)
            {
                if (storable is X converted)
                {
                    result.Result.Add(converted);
                }
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
