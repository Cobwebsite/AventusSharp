using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

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
        protected override bool SupportsExternalExpressions => true;

        public DatabaseExistBuilderInfo? info = null;
        public DatabaseExistBuilder(IDBStorage storage, IGenericDM DM, Type? baseType = null) : base(storage, DM, baseType)
        {

        }

        public IExistBuilder<T> Where(Expression<Func<T, bool>> func)
        {
            WhereGeneric(func);
            return this;
        }

        public IExistBuilder<T> OrWhere(Expression<Func<T, bool>> func)
        {
            OrWhereGeneric(func);
            return this;
        }

        public ExistBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func)
        {
            WhereGenericWithParameters(func);
            return new(this);
        }
        public async Task<bool> Run()
        {
            ResultWithError<bool> result = await RunWithError();
            if (result.Success)
            {
                return result.Result;
            }
            return false;
        }

        public async Task<ResultWithError<bool>> RunWithError()
        {
            List<GenericError> runErrors = GetRunErrors();
            if (runErrors.Count > 0)
            {
                return new ResultWithError<bool>()
                {
                    Errors = runErrors
                };
            }
            if (RequiresPostProcessing)
            {
                DatabaseQueryBuilder<T> query = new DatabaseQueryBuilder<T>(Storage, DM);
                for (int i = 0; i < QueryPredicates.Count; i++)
                {
                    (Expression<Func<T, bool>> Predicate, WhereGroupFctEnum Link) predicate = QueryPredicates[i];
                    if (i > 0 && predicate.Link == WhereGroupFctEnum.Or)
                    {
                        query.OrWhere(predicate.Predicate);
                    }
                    else
                    {
                        query.Where(predicate.Predicate);
                    }
                }
                query.Limit(1);

                ResultWithError<List<T>> matches = await query.RunWithError();
                ResultWithError<bool> existence = new ResultWithError<bool>
                {
                    Errors = matches.Errors,
                    Result = matches.Result?.Any() == true
                };
                DM.PrintErrors(existence);
                return existence;
            }
            ResultWithError<bool> result = await Storage.ExistFromBuilder(this);
            DM.PrintErrors(result);
            return result;
        }

        void IExistBuilder<T>.PrepareInternal(params object[] objects)
        {
            PrepareGeneric(objects);
        }

        void IExistBuilder<T>.SetVariableInternal(string name, object value)
        {
            SetVariableGeneric(name, value);
        }
        void IExistBuilder<T>.ResetPreparedParametersInternal()
        {
            ResetPreparedParametersGeneric();
        }
    }
}
