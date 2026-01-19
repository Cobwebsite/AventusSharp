using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;

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

        public List<T> Run()
        {
            ResultWithError<List<T>> result = Storage.QueryFromBuilder(this);
            if (result.Success && result.Result != null)
            {
                return result.Result;
            }
            return new List<T>();
        }
        public ResultWithError<List<T>> RunWithError()
        {
            MergeScopeAndWhere();
            var result = Storage.QueryFromBuilder(this);
            DM.PrintErrors(result);
            return result;

        }

        public T? Single()
        {
            return SingleWithError().Result;
        }
        public ResultWithError<T> SingleWithError()
        {
            ResultWithError<T> result = new ResultWithError<T>();
            ResultWithError<List<T>> runResult = RunWithError();
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

        public QueryBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> expression)
        {
            WhereGenericWithParameters(expression);
            if (prepared == null)
            {
                prepared = new(this);
            }
            return prepared;
        }

        public IQueryBuilder<T> Field<U>(Expression<Func<T, U>> expression)
        {
            FieldGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> Fields()
        {
            FieldsGeneric();
            return this;
        }

        public IQueryBuilder<T> Ignore<U>(Expression<Func<T, U>> expression)
        {
            IgnoreGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> Sort<U>(Expression<Func<T, U>> expression, Sort? sort)
        {
            SortGeneric(expression, sort ?? DB.Sort.ASC);
            return this;
        }

        public IQueryBuilder<T> Group<U>(Expression<Func<T, U>> expression)
        {
            GroupGeneric(expression);
            return this;
        }

        public IQueryBuilder<T> Include(Expression<Func<T, IStorable>> expression)
        {
            IncludeGeneric(expression);
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
    }

}
