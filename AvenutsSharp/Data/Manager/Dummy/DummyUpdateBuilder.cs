using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager.Dummy
{
    public class DummyUpdateBuilder<T> : IUpdateBuilder<T>
    {
        public IUpdateBuilder<T> Field<U>(Expression<Func<T, U>> fct)
        {
            throw new NotImplementedException();
        }


        public List<T>? Run(T item)
        {
            throw new NotImplementedException();
        }

        public ResultWithError<List<T>> RunWithError(T item)
        {
            throw new NotImplementedException();
        }

        public T? Single(T item)
        {
            throw new NotImplementedException();
        }

        public ResultWithError<T> SingleWithError(T item)
        {
            throw new NotImplementedException();
        }

        public IUpdateBuilder<T> Where(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }

        public UpdateBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }

        void IUpdateBuilder<T>.PrepareInternal(params object[] objects)
        {
            throw new NotImplementedException();
        }

        void IUpdateBuilder<T>.SetVariableInternal(string name, object value)
        {
            throw new NotImplementedException();
        }
    }
}
