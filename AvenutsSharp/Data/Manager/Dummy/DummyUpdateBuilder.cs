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

        public IUpdateBuilder<T> Prepare(params object[] objects)
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

        public ResultWithError<T> RunWithErrorSingle(T item)
        {
            throw new NotImplementedException();
        }

        public IUpdateBuilder<T> SetVariable(string name, object value)
        {
            throw new NotImplementedException();
        }

        public IUpdateBuilder<T> Where(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }

        public IUpdateBuilder<T> WhereWithParameters(Expression<Func<T, bool>> func)
        {
            throw new NotImplementedException();
        }
    }
}
