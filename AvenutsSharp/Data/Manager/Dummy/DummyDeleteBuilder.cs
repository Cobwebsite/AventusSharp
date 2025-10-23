using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager.Dummy
{
    public class DummyDeleteBuilder<T> : IDeleteBuilder<T>
    {

        public List<T>? Run()
        {
            throw new NotImplementedException();
        }

        public ResultWithError<List<T>> RunWithError()
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

        void IDeleteBuilder<T>.PrepareInternal(params object[] objects)
        {
            throw new NotImplementedException();
        }

        void IDeleteBuilder<T>.SetVariableInternal(string name, object value)
        {
            throw new NotImplementedException();
        }
    }
}
