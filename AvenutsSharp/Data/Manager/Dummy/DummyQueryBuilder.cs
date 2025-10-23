using AventusSharp.Data.Manager.DB;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager.Dummy
{
    public class DummyQueryBuilder<T> : IQueryBuilder<T>
    {

        public IQueryBuilder<T> Fields()
        {
            throw new NotImplementedException();
        }
        public IQueryBuilder<T> Field<U>(Expression<Func<T, U>> memberExpression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Ignore<U>(Expression<Func<T, U>> memberExpression)
        {
            throw new NotImplementedException();
        }
        
        public IQueryBuilder<T> Include(Expression<Func<T, IStorable>> memberExpression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Limit(int? limit)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Offset(int? offset)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Prepare(params object[] objects)
        {
            throw new NotImplementedException();
        }

        public List<T> Run()
        {
            throw new NotImplementedException();
        }

        public ResultWithError<List<T>> RunWithError()
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> SetVariable(string name, object value)
        {
            throw new NotImplementedException();
        }

        public T? Single()
        {
            throw new NotImplementedException();
        }

        public ResultWithError<T> SingleWithError()
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Sort<U>(Expression<Func<T, U>> expression, Sort? sort)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Group<U>(Expression<Func<T, U>> expression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Take(int length)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Take(int length, int offset)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Where(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }


        public QueryBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }

        void IQueryBuilder<T>.PrepareInternal(params object[] objects)
        {
            throw new NotImplementedException();
        }

        void IQueryBuilder<T>.SetVariableInternal(string name, object value)
        {
            throw new NotImplementedException();
        }
        
    }
}
