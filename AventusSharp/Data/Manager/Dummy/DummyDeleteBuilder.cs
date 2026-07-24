using AventusSharp.Data.Attributes;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager.Dummy
{
    public class DummyDeleteBuilder<T> : IDeleteBuilder<T>
    {
        public Task<List<T>?> Run()
        {
            throw new NotImplementedException();
        }

        public Task<ResultWithError<List<T>>> RunWithError()
        {
            throw new NotImplementedException();
        }

        public IDeleteBuilder<T> Where(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }

        public DeleteBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }

        public IDeleteBuilder<T> WithoutScope()
        {
            throw new NotImplementedException();
        }

        public IDeleteBuilder<T> WithScope<X>() where X : IScope, new()
        {
            throw new NotImplementedException();
        }

        void IDeleteBuilder<T>.PrepareInternal(params object[] objects)
        {
            throw new NotImplementedException();
        }

        Task<List<T>?> IDeleteBuilder<T>.Run()
        {
            throw new NotImplementedException();
        }

        Task<ResultWithError<List<T>>> IDeleteBuilder<T>.RunWithError()
        {
            throw new NotImplementedException();
        }

        void IDeleteBuilder<T>.SetVariableInternal(string name, object value)
        {
            throw new NotImplementedException();
        }

        IDeleteBuilder<T> IDeleteBuilder<T>.Where(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }

        DeleteBuilderPrepared<T> IDeleteBuilder<T>.WhereWithParameters(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }
    }
}
