using AventusSharp.Data.Attributes;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager.DB.Builders
{
    public class DatabaseQueryBuilderInfo
    {
        public string Sql;

        public DatabaseQueryBuilderInfo(string sql)
        {
            Sql = sql;
        }
    }


    public class DatabaseQueryBuilder<T> : DatabaseGenericBuilder<T>, IQueryBuilder<T>, ILambdaTranslatable where T : IStorable
    {

        public DatabaseQueryBuilderInfo? info = null;
        public bool UseShortObject { get; set; } = true;

        private QueryBuilderPrepared<T>? prepared = null;

        public DatabaseQueryBuilder(IDBStorage storage, IGenericDM DM) : base(storage, DM)
        {

        }

        public async Task<List<T>> Run()
        {
            ResultWithError<List<T>> result = await RunWithError();
            return result.Result ?? new List<T>();
        }
        public async Task<ResultWithError<List<T>>> RunWithError()
        {
            if (Errors.Count > 0)
            {
                return new ResultWithError<List<T>>()
                {
                    Errors = Errors
                };
            }
            MergeScopeAndWhere();
            var result = await Storage.QueryFromBuilder(this);
            DM.PrintErrors(result);
            return result;

        }

        public async Task<VoidWithError> RunStreamWithError(Func<T, Task<VoidWithError>> action)
        {
            if (Errors.Count > 0)
            {
                return new VoidWithError()
                {
                    Errors = Errors
                };
            }
            MergeScopeAndWhere();
            VoidWithError result = await Storage.QueryStreamFromBuilder(this, action);
            DM.PrintErrors(result);
            return result;

        }

        public async Task<T?> Single()
        {
            return (await SingleWithError()).Result;
        }
        public async Task<ResultWithError<T>> SingleWithError()
        {
            ResultWithError<T> result = new ResultWithError<T>();
            ResultWithError<List<T>> runResult = await RunWithError();
            result.Errors = runResult.Errors;
            if (runResult.Result != null && runResult.Result.Count > 0)
            {
                result.Result = runResult.Result[0];
            }
            return result;

        }

        public IQueryBuilder<T> Where(Expression<Func<T, bool>> expression)
        {
            WhereGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> OrWhere(Expression<Func<T, bool>> expression)
        {
            OrWhereGeneric(expression);
            return this;
        }

        private Dictionary<Type, object?> _searchable = new();
        private (bool success, object? value) TryGetSearchValue(Type type, string search)
        {
            if (!_searchable.ContainsKey(type))
            {
                try
                {
                    var converter = TypeDescriptor.GetConverter(type);
                    if (converter != null && converter.CanConvertFrom(typeof(string)))
                    {
                        _searchable[type] = converter.ConvertFromString(search);
                    }
                    else
                    {
                        _searchable[type] = null;
                    }
                }
                catch
                {
                    _searchable[type] = null; // Conversion impossible (ex: "abc" vers int)
                }
            }

            object? val = _searchable[type];
            return (val != null, val);
        }

        public IQueryBuilder<T> Where(string search, List<string> fields)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

            Expression? finalBody = null;

            if (string.IsNullOrEmpty(search)) return this; // Ou logique spécifique

            var table = InfoByPath[""];

            foreach (var field in fields)
            {
                TableMemberInfoSql? member = table.TableInfo.Members.FirstOrDefault(p => p.Name == field);
                MemberInfo? memberInfo = member?.memberInfo;

                if (memberInfo == null || member == null)
                {
                    Errors.Add(new DataError(DataErrorCode.MemberNotFound, "Can't find the field " + field + " on the object " + typeof(T).Name));
                    return this;
                }

                MemberExpression propertyAccess;

                if (memberInfo is PropertyInfo propertyInfo)
                {
                    propertyAccess = Expression.Property(parameter, propertyInfo);
                }
                else if (memberInfo is FieldInfo fieldInfo)
                {
                    propertyAccess = Expression.Field(parameter, fieldInfo);
                }
                else
                {
                    Errors.Add(new DataError(DataErrorCode.UnknowError, "Impossible"));
                    return this;
                }

                Expression? fieldCondition = null;

                if (member.MemberType == typeof(string))
                {
                    MethodInfo containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
                    ConstantExpression searchConstant = Expression.Constant(search, typeof(string));

                    // TODO check if null maybe it will crash
                    fieldCondition = Expression.Call(propertyAccess, containsMethod, searchConstant);
                }
                else if (member.MemberType == typeof(DateTime) || member.MemberType == typeof(DateTime?))
                {
                    if (DateTime.TryParse(search, out DateTime dateValue))
                    {
                        fieldCondition = Expression.Equal(
                            propertyAccess,
                            Expression.Convert(Expression.Constant(dateValue), member.MemberType)
                        );
                    }
                }
                else
                {
                    var result = TryGetSearchValue(member.MemberType, search);

                    if (result.success)
                    {
                        fieldCondition = Expression.Equal(
                            propertyAccess,
                            Expression.Convert(Expression.Constant(result.value), member.MemberType)
                        );
                    }
                }

                if (fieldCondition != null)
                {
                    if (finalBody == null)
                    {
                        finalBody = fieldCondition;
                    }
                    else
                    {
                        finalBody = Expression.OrElse(finalBody, fieldCondition);
                    }
                }
            }

            if (finalBody == null)
            {
                Errors.Add(new DataError(
                    DataErrorCode.WrongType,
                    $"The search value '{search}' cannot be converted for any selected field on {typeof(T).Name}"));
                return this;
            }

            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(finalBody, parameter);

            return Where(lambda);
        }

        public QueryBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> expression)
        {
            WhereGenericWithParameters(expression);
            if (prepared == null)
            {
                prepared = new(this);
            }
            return prepared;
        }

        public IQueryBuilder<T> Field<U>(Expression<Func<T, U?>> expression)
        {
            FieldGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> Field(LambdaExpression expression)
        {
            FieldGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> Fields()
        {
            FieldsGeneric();
            return this;
        }

        public IQueryBuilder<T> Ignore<U>(Expression<Func<T, U?>> expression)
        {
            IgnoreGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> Ignore(LambdaExpression expression)
        {
            IgnoreGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> Sort<U>(Expression<Func<T, U?>> expression, Sort? sort)
        {
            SortGeneric(expression, sort ?? DB.Sort.ASC);
            return this;
        }
        public IQueryBuilder<T> Sort(LambdaExpression expression, Sort? sort)
        {
            SortGeneric(expression, sort ?? DB.Sort.ASC);
            return this;
        }

        public IQueryBuilder<T> Group<U>(Expression<Func<T, U?>> expression)
        {
            GroupGeneric(expression);
            return this;
        }
        public IQueryBuilder<T> Group(LambdaExpression expression)
        {
            GroupGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> Include<Y>(Expression<Func<T, Y?>> expression, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), null);
            return this;
        }
        public IQueryBuilder<T> Include<Y>(Expression<Func<T, List<Y>?>> expression, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), null);
            return this;
        }
        IQueryBuilder<T> IQueryBuilder<T>.Include(LambdaExpression expression, List<LambdaExpression>? fields)
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), null);
            return this;
        }

        public IQueryBuilder<T> IncludeWithoutScope<Y>(Expression<Func<T, Y?>> expression, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), []);
            return this;
        }
        public IQueryBuilder<T> IncludeWithoutScope<Y>(Expression<Func<T, List<Y>?>> expression, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), []);
            return this;
        }
        IQueryBuilder<T> IQueryBuilder<T>.IncludeWithoutScope(LambdaExpression expression, List<LambdaExpression>? fields)
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), []);
            return this;
        }


        public IQueryBuilder<T> IncludeWithScope<Y>(Expression<Func<T, Y?>> expression, List<Scope<Y>> scopes, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), scopes);
            return this;
        }
        public IQueryBuilder<T> IncludeWithScope<Y>(Expression<Func<T, Y?>> expression, Scope<Y> scope, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), [scope]);
            return this;
        }
        public IQueryBuilder<T> IncludeWithScope<Y>(Expression<Func<T, List<Y>?>> expression, List<Scope<Y>> scopes, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), scopes.ConvertList<IScope>());
            return this;
        }
        public IQueryBuilder<T> IncludeWithScope<Y>(Expression<Func<T, List<Y>?>> expression, Scope<Y> scope, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), [scope]);
            return this;
        }
        IQueryBuilder<T> IQueryBuilder<T>.IncludeWithScope(LambdaExpression expression, List<IScope> scopes, List<LambdaExpression>? fields)
        {
            IncludeGeneric(expression, fields?.ConvertList<LambdaExpression>(), scopes);
            return this;
        }

        public IQueryBuilder<T> Limit(int? limit)
        {
            LimitGeneric(limit);
            return this;
        }

        public IQueryBuilder<T> Offset(int? offset)
        {
            OffsetGeneric(offset);
            return this;
        }

        public IQueryBuilder<T> Take(int length)
        {
            Limit(length);
            return this;
        }
        public IQueryBuilder<T> Take(int length, int offset)
        {
            Limit(length);
            Offset(offset);
            return this;
        }

        internal void PrepareInternal(params object[] objects)
        {
            PrepareGeneric(objects);
        }

        internal void SetVariableInternal(string name, object value)
        {
            SetVariableGeneric(name, value);
        }
        void IQueryBuilder<T>.PrepareInternal(params object[] objects)
        {
            PrepareGeneric(objects);
        }

        void IQueryBuilder<T>.SetVariableInternal(string name, object value)
        {
            SetVariableGeneric(name, value);
        }

        public IQueryBuilder<T> WithScope<X>() where X : IScope, new()
        {
            WithScopeGeneric<X>();
            return this;
        }
        public IQueryBuilder<T> WithScope(IScope scope)
        {
            WithScopeGeneric(scope);
            return this;
        }
        public IQueryBuilder<T> WithoutScope()
        {
            WithoutScopeGeneric();
            return this;
        }
    }

}
