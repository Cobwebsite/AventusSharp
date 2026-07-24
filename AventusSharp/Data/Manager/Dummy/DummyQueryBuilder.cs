using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager.Dummy
{
    public class DummyQueryBuilder<T> : IQueryBuilder<T>
    {
        public IQueryBuilder<T> Field<U>(Expression<Func<T, U?>> memberExpression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Field(LambdaExpression memberExpression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Fields()
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Group<U>(Expression<Func<T, U?>> expression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Group(LambdaExpression expression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Ignore<U>(Expression<Func<T, U?>> memberExpression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Ignore(LambdaExpression memberExpression)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Include<Y>(Expression<Func<T, Y?>> memberExpression, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Include<Y>(Expression<Func<T, List<Y>?>> memberExpression, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
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

        public Task<List<T>> Run()
        {
            throw new NotImplementedException();
        }

        public Task<ResultWithError<List<T>>> RunWithError()
        {
            throw new NotImplementedException();
        }

        public Task<VoidWithError> RunStreamWithError(Func<T, Task<VoidWithError>> action)
        {
            throw new NotImplementedException();
        }

        public Task<T?> Single()
        {
            throw new NotImplementedException();
        }

        public Task<ResultWithError<T>> SingleWithError()
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Sort<U>(Expression<Func<T, U?>> expression, Sort? sort)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> Sort(LambdaExpression expression, Sort? sort)
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

        public IQueryBuilder<T> Where(string search, List<string> fields)
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

        IQueryBuilder<T> IQueryBuilder<T>.Include(LambdaExpression expression, List<LambdaExpression>? fields)
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> WithScope<X>() where X : IScope, new()
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> WithoutScope()
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> IncludeWithoutScope<Y>(Expression<Func<T, Y?>> memberExpression, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> IncludeWithoutScope<Y>(Expression<Func<T, List<Y>?>> memberExpression, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> IncludeWithScope<Y>(Expression<Func<T, Y?>> memberExpression, List<Scope<Y>> scopes, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> IncludeWithScope<Y>(Expression<Func<T, Y?>> memberExpression, Scope<Y> scope, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> IncludeWithScope<Y>(Expression<Func<T, List<Y>?>> memberExpression, List<Scope<Y>> scopes, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> IncludeWithScope<Y>(Expression<Func<T, List<Y>?>> memberExpression, Scope<Y> scope, List<Expression<Func<Y, object?>>>? fields = null) where Y : IStorable
        {
            throw new NotImplementedException();
        }

        public IQueryBuilder<T> WithScope(IScope scope)
        {
            throw new NotImplementedException();
        }

        IQueryBuilder<T> IQueryBuilder<T>.IncludeWithoutScope(LambdaExpression memberExpression, List<LambdaExpression>? fields)
        {
            throw new NotImplementedException();
        }

        IQueryBuilder<T> IQueryBuilder<T>.IncludeWithScope(LambdaExpression memberExpression, List<IScope> scopes, List<LambdaExpression>? fields)
        {
            throw new NotImplementedException();
        }
    }
}
