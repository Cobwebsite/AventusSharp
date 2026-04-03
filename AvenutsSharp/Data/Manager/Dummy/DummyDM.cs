using AventusSharp.Data.Migrations;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager.Dummy
{
    public class DummyDM<U> : GenericDM<DummyDM<U>, U> where U : IStorable
    {
        private readonly Dictionary<int, U> Records = new();

        public override IDeleteBuilder<X> CreateDelete<X>()
        {
            return new DummyDeleteBuilder<X>();
        }

        public override IQueryBuilder<X> CreateQuery<X>()
        {
            return new DummyQueryBuilder<X>();
        }

        public override IUpdateBuilder<X> CreateUpdate<X>()
        {
            return new DummyUpdateBuilder<X>();
        }

        protected override Task<ResultWithError<List<X>>> CreateLogic<X>(List<X> values)
        {
            ResultWithError<List<X>> result = new ResultWithError<List<X>>();
            result.Result = new List<X>();
            int? errorOn = null;
            foreach (X x in values)
            {
                if (!Records.ContainsKey(x.Id))
                {
                    Records.Add(x.Id, x);
                    result.Result.Add(x);
                }
                else
                {
                    errorOn = x.Id;
                    result.Errors.Add(new DataError(DataErrorCode.ItemAlreadyExist, "An item with the key " + x.Id + " is already present"));
                }
            }
            if (errorOn != null)
            {
                foreach (X x in result.Result)
                {
                    if (Records.ContainsKey(x.Id))
                    {
                        Records.Remove(x.Id);
                    }
                }
            }

            return Task.FromResult(result);
        }

        protected override Task<VoidWithError> BulkCreateLogic<X>(List<X> values, bool withId)
        {
            VoidWithError result = new();
            foreach (X x in values)
            {
                Records[x.Id] = x;
            }
            return Task.FromResult(result);;
        }

        protected override Task<ResultWithError<List<X>>> DeleteLogic<X>(List<X> values)
        {
            ResultWithError<List<X>> result = new ResultWithError<List<X>>();
            result.Result = new List<X>();
            foreach (X x in values)
            {
                if (Records.ContainsKey(x.Id) && Records[x.Id] is X casted)
                {
                    result.Result.Add(casted);
                    Records.Remove(x.Id);
                }
            }
            return Task.FromResult(result);;
        }

        protected override Task<ResultWithError<List<X>>> GetAllLogic<X>()
        {
            ResultWithError<List<X>> result = new()
            {
                Result = new List<X>()
            };
            foreach (U item in Records.Values.ToList())
            {
                if (item is X casted)
                {
                    result.Result.Add(casted);
                }
            }
            return Task.FromResult(result);;
        }

        protected override Task<ResultWithError<X>> GetByIdLogic<X>(int id)
        {
            ResultWithError<X> result = new();
            if (Records.ContainsKey(id) && Records[id] is X casted)
            {
                result.Result = casted;
            }
            else
            {
                result.Errors.Add(new DataError(DataErrorCode.ItemNoExistInsideStorage, "No item found with the key " + id + " as " + typeof(X).Name));
            }
            return Task.FromResult(result);;
        }

        protected override async Task<ResultWithError<List<X>>> GetByIdsLogic<X>(List<int> ids)
        {
            ResultWithError<List<X>> result = new()
            {
                Result = new List<X>()
            };
            foreach (int id in ids)
            {
                ResultWithError<X> resultTemp = await GetByIdLogic<X>(id);
                if (resultTemp.Success && resultTemp.Result != null)
                {
                    result.Result.Add(resultTemp.Result);
                }
                else
                {
                    result.Errors.AddRange(resultTemp.Errors);
                }
            }
            return result;
        }

        protected override Task<VoidWithError> Initialize()
        {
            return Task.FromResult(new VoidWithError());
        }
        internal override IMigrationProvider GetMigrationProvider()
        {
            return MigrationFactory.Make<DummyMigrationProvider>();
        }
        private readonly Dictionary<Type, object> savedUpdateQuery = new();
        protected override async Task<ResultWithError<List<X>>> UpdateLogic<X>(List<X> values)
        {
            ResultWithError<List<X>> result = new()
            {
                Result = new List<X>()
            };
            int id = 0;
            foreach (X value in values)
            {
                Type type = value.GetType();
                if (!savedUpdateQuery.ContainsKey(type))
                {
                    DummyUpdateBuilder<X> query = new();
                    savedUpdateQuery[type] = query.WhereWithParameters(p => p.Id == id);;
                }

                ResultWithError<List<X>> resultTemp = await ((UpdateBuilderPrepared<X>)savedUpdateQuery[type]).New().Prepare(value.Id).RunWithError(value);
                if (resultTemp.Success && resultTemp.Result?.Count > 0)
                {
                    result.Result.Add(resultTemp.Result[0]);
                }
                else
                {
                    result.Errors.AddRange(resultTemp.Errors);
                }
            }
            return result;
        }

        protected override Task<ResultWithError<List<X>>> WhereLogic<X>(Expression<Func<X, bool>> func)
        {
            ResultWithError<List<X>> result = new()
            {
                Result = new List<X>()
            };
            Func<X, bool> fct = func.Compile();
            foreach (KeyValuePair<int, U> pair in Records)
            {
                try
                {
                    if (pair.Value is X casted)
                    {
                        if (fct.Invoke(casted))
                        {
                            result.Result.Add(casted);
                        }
                    }
                }
                catch (Exception e)
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
            }
            return Task.FromResult(result);
        }

        public override IExistBuilder<X> CreateExist<X>()
        {
            throw new NotImplementedException();
        }

        #region Transaction
        private AsyncLocal<DummyTransactionContext?> _transactionScope = new();
        private DummyTransactionContext? transactionScope
        {
            get => _transactionScope.Value;
            set => _transactionScope.Value = transactionScope;
        }
        protected override TransactionContext? getTransactionScope()
        {
            return transactionScope;
        }
        protected override void setTransactionScope(TransactionContext? context)
        {
            if (context == null)
                transactionScope = null;
            else if (context is DummyTransactionContext dummyTransactionContext)
                transactionScope = dummyTransactionContext;
        }
        protected override Task<ResultWithError<TransactionContext>> BeginTransactionScope()
        {
            return Task.FromResult(new ResultWithError<TransactionContext>());
        }
        protected override Task EndTransactionScope()
        {
            return Task.CompletedTask;
        }
        #endregion

    }
}
