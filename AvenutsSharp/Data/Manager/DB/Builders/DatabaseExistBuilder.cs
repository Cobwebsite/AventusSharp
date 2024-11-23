using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager.DB.Builders
{
    public class DatabaseExistBuilderInfo
    {
        public string Sql;

        public DatabaseExistBuilderInfo(string sql)
        {
            Sql = sql;
        }
    }
    public class DatabaseExistBuilder<T> : DatabaseGenericBuilder<T>, IExistBuilder<T> where T : IStorable
    {

        public DatabaseExistBuilderInfo? info = null;
        public DatabaseExistBuilder(IDBStorage storage, IGenericDM DM, Type? baseType = null) : base(storage, DM, baseType)
        {

        }

        public IExistBuilder<T> Where(Expression<Func<T, bool>> func)
        {
            WhereGeneric(func);
            return this;
        }

        public IExistBuilder<T> WhereWithParameters(Expression<Func<T, bool>> func)
        {
            WhereGenericWithParameters(func);
            return this;
        }
        public IExistBuilder<T> Prepare(params object[] objects)
        {
            PrepareGeneric(objects);
            return this;
        }
        public IExistBuilder<T> SetVariable(string name, object value)
        {
            SetVariableGeneric(name, value);
            return this;
        }
        public bool Run()
        {
            ResultWithError<bool> result = Storage.ExistFromBuilder(this);
            if (result.Success)
            {
                return result.Result;
            }
            return false;
        }

        public ResultWithError<bool> RunWithError()
        {
            ResultWithError<bool> result = Storage.ExistFromBuilder(this);
            DM.PrintErrors(result);
            return result;
        }

    }
}
