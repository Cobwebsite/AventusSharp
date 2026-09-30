using AventusSharp.Localization;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager.DB.Builders
{
    internal interface IQuerySqlCache
    {
        void InvalidateSql();
        void SetEvaluationMode(bool enabled);
    }

    public class DatabaseQueryBuilderInfo
    {
        public string Sql;

        public DatabaseQueryBuilderInfo(string sql)
        {
            Sql = sql;
        }
    }


    public class DatabaseQueryBuilder<T> : DatabaseGenericBuilder<T>, IQueryBuilder<T>, ILambdaTranslatable, IQuerySqlCache where T : IStorable
    {
        public void InvalidateSql() => info = null;
        internal bool SuppressCacheRegistration { get; private set; }
        private bool? savedCanonicalCache;
        public void SetEvaluationMode(bool enabled)
        {
            if (enabled)
            {
                savedCanonicalCache ??= UseCanonicalCache;
                UseCanonicalCache = false;
                SuppressCacheRegistration = true;
            }
            else if (savedCanonicalCache.HasValue)
            {
                UseCanonicalCache = savedCanonicalCache.Value;
                savedCanonicalCache = null;
                SuppressCacheRegistration = false;
            }
            foreach (DatabaseSubBuilder subQuery in SubQueries.Values)
            {
                subQuery.SetEvaluationMode(enabled);
            }
        }
        protected override bool SupportsExternalExpressions => true;

        public DatabaseQueryBuilderInfo? info = null;
        public bool UseShortObject { get; set; } = true;
        public bool UseCanonicalCache { get; set; } = false;

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
            List<GenericError> runErrors = GetRunErrors();
            if (runErrors.Count > 0)
            {
                return new ResultWithError<List<T>>()
                {
                    Errors = runErrors
                };
            }
            MergeScopeAndWhere();
            List<LambdaExpression> ignoredProjection = IgnoredExpressions.ToList();
            bool defaultProjection = AllMembersByPath.TryGetValue("", out bool allMembers) && allMembers;
            bool restoreIgnoredProjection = ignoredProjection.Count > 0 &&  (RequiresPostProcessing || RequiresPostSort || RequiresPostGroup);
            
            if (RequiresPostProcessing || RequiresPostSort || RequiresPostGroup)
            {
                foreach (var predicate in QueryPredicates)
                {
                    ExternalExpressionLoader<T>.LoadFields(this, predicate.Predicate);
                }
                foreach (var scope in QueryScopes)
                {
                    ExternalExpressionLoader<T>.LoadFields(this, scope);
                }
                foreach (var sort in QuerySorts)
                {
                    ExternalExpressionLoader<T>.LoadFields(this, sort.Expression);
                }
                foreach (var group in QueryGroups)
                {
                    ExternalExpressionLoader<T>.LoadFields(this, group);
                }
            }

            if (defaultProjection && ignoredProjection.Count == 0 &&
                (RequiresPostProcessing || RequiresPostSort || RequiresPostGroup) &&
                InfoByPath[""].TableInfo.Primary is TableMemberInfoSql primary &&
                !InfoByPath[""].Members.ContainsKey(primary))
            {
                DatabaseBuilderInfo root = InfoByPath[""];
                root.Members[primary] = new DatabaseBuilderInfoMember(primary, root.Alias, Storage);
            }

            if (restoreIgnoredProjection)
            {
                info = null;
            }

            if (restoreIgnoredProjection)
            {
                SetEvaluationMode(true);
            }

            ResultWithError<List<T>> result;
            try
            {
                result = await Storage.QueryFromBuilder(this);
            }
            finally
            {
                if (restoreIgnoredProjection)
                {
                    SetEvaluationMode(false);
                }
            }

            if (result.Success && result.Result != null && (RequiresPostProcessing || RequiresPostSort || RequiresPostGroup))
            {
                try
                {
                    result.Result = ProcessExternalExpressions(result.Result);
                    if (restoreIgnoredProjection && result.Result.Count > 0)
                    {
                        // Fetch the selected rows again with the requested projection.
                        // The evaluation-only fields must not leak into the result.
                        List<int> selectedIds = result.Result.Select(item => item.Id).ToList();
                        foreach (LambdaExpression ignored in ignoredProjection)
                        {
                            IgnoreGeneric(ignored, record: false);
                        }
                        
                        info = null;
                        ResultWithError<List<T>> projected = await Storage.QueryFromBuilder(this);
                        result.Errors.AddRange(projected.Errors);
                        if (projected.Success && projected.Result != null)
                        {
                            ILookup<int, T> byId = projected.Result.ToLookup(item => item.Id);
                            result.Result = selectedIds
                                .Select(id => byId[id].FirstOrDefault())
                                .Where(item => item != null)
                                .Select(item => item!)
                                .ToList();
                        }
                    }
                }
                catch (Exception exception)
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, exception));
                }
            }

            DM.PrintErrors(result);
            return result;

        }

        public async Task<VoidWithError> RunStreamWithError(Func<T, Task<VoidWithError>> action)
        {
            List<GenericError> runErrors = GetRunErrors();
            if (runErrors.Count > 0)
            {
                return new VoidWithError()
                {
                    Errors = runErrors
                };
            }

            MergeScopeAndWhere();
            if (RequiresPostProcessing || RequiresPostSort || RequiresPostGroup)
            {
                ResultWithError<List<T>> loaded = await RunWithError();
                VoidWithError processed = new VoidWithError { Errors = loaded.Errors };

                if (!processed.Success || loaded.Result == null)
                    return processed;

                foreach (T item in loaded.Result)
                {
                    VoidWithError callback = await action(item);
                    processed.Errors.AddRange(callback.Errors);

                    if (!callback.Success)
                        break;
                }
                return processed;
            }

            VoidWithError result = await Storage.QueryStreamFromBuilder(this, action);
            DM.PrintErrors(result);
            return result;

        }

        private List<T> ProcessExternalExpressions(List<T> items)
        {
            IEnumerable<T> selected = items;
            if (RequiresPostProcessing)
            {
                List<(Func<T, bool> Evaluate, WhereGroupFctEnum Link)> predicates = QueryPredicates
                    .Select(item => (Evaluate: item.Predicate.Compile(), item.Link))
                    .ToList();

                List<Func<T, bool>> scopes = QueryScopes.Select(scope => scope.Compile()).ToList();

                selected = selected.Where(item =>
                {
                    bool matches = predicates.Count == 0;
                    for (int i = 0; i < predicates.Count; i++)
                    {
                        (Func<T, bool> Evaluate, WhereGroupFctEnum Link) predicate = predicates[i];
                        bool value = SafePredicate(predicate.Evaluate, item);
                        if (predicate.Link == WhereGroupFctEnum.Or)
                        {
                            matches = matches || value;
                        }
                        else if (i == 0)
                        {
                            matches = value;
                        }
                        else
                        {
                            matches = matches && value;
                        }
                    }
                    return matches && scopes.All(scope => SafePredicate(scope, item));
                });
            }

            if (RequiresPostProcessing || RequiresPostGroup)
            {
                List<Func<T, object?>> keys = QueryGroups.Select(CompileKey).ToList();
                if (keys.Count > 0)
                {
                    selected = selected
                                .GroupBy(item =>
                                    keys.Select(key => SafeKey(key, item)).ToArray(),
                                    StructuralKeyComparer.Instance
                                )
                                .Select(group => group.First());

                }
            }

            if (RequiresPostProcessing || RequiresPostSort || RequiresPostGroup)
            {
                IOrderedEnumerable<T>? ordered = null;
                foreach ((LambdaExpression Expression, Sort Direction) sort in QuerySorts)
                {
                    Func<T, object?> key = CompileKey(sort.Expression);
                    if (ordered == null)
                    {
                        if (sort.Direction == DB.Sort.ASC)
                        {
                            ordered = selected.OrderBy(item => SafeKey(key, item), ObjectKeyComparer.Instance);
                        }
                        else
                        {
                            ordered = selected.OrderByDescending(item => SafeKey(key, item), ObjectKeyComparer.Instance);
                        }
                    }
                    else
                    {
                        if (sort.Direction == DB.Sort.ASC)
                        {
                            ordered = ordered.ThenBy(item => SafeKey(key, item), ObjectKeyComparer.Instance);
                        }
                        else
                        {
                            ordered = ordered.ThenByDescending(item => SafeKey(key, item), ObjectKeyComparer.Instance);
                        }
                    }
                }

                if (ordered != null)
                {
                    selected = ordered;
                }

                if (OffsetSize.HasValue)
                {
                    selected = selected.Skip(OffsetSize.Value);
                }

                if (LimitSize.HasValue)
                {
                    selected = selected.Take(LimitSize.Value);
                }
            }
            return selected.ToList();
        }

        private static bool SafePredicate(Func<T, bool> predicate, T item)
        {
            try
            {
                return predicate(item);
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }

        private static object? SafeKey(Func<T, object?> key, T item)
        {
            try
            {
                return key(item);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        private static Func<T, object?> CompileKey(LambdaExpression expression)
        {
            Expression body = Expression.Convert(expression.Body, typeof(object));
            return Expression.Lambda<Func<T, object?>>(body, expression.Parameters).Compile();
        }

        private sealed class StructuralKeyComparer : IEqualityComparer<object?[]>
        {
            public static readonly StructuralKeyComparer Instance = new();
            public bool Equals(object?[]? x, object?[]? y) => StructuralComparisons.StructuralEqualityComparer.Equals(x, y);
            public int GetHashCode(object?[] value) => StructuralComparisons.StructuralEqualityComparer.GetHashCode(value);
        }

        private sealed class ObjectKeyComparer : IComparer<object?>
        {
            public static readonly ObjectKeyComparer Instance = new();
            public int Compare(object? x, object? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return Comparer.DefaultInvariant.Compare(x, y);
            }
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
                    Errors.Add(new DataError(DataErrorCode.MemberNotFound, AventusTranslations.Get(AventusMessageKeys.Data.FieldNotFound, field, typeof(T).Name)));
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
                    Errors.Add(new DataError(DataErrorCode.UnknownError, AventusTranslations.Get(AventusMessageKeys.Data.UnexpectedState)));
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
                    AventusTranslations.Get(AventusMessageKeys.Data.SearchValueConversionFailed, search, typeof(T).Name)));
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
        void IQueryBuilder<T>.ResetPreparedParametersInternal()
        {
            ResetPreparedParametersGeneric();
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
