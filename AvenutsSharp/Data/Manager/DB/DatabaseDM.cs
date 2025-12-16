using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager.DB
{
    public interface IDatabaseDM
    {
        public bool NeedLocalCache { get; }
        public bool IsShortLink(string path);
        public IDBStorage Storage { get; }
        public List<X> RemoveRecordsItems<X>(List<int> ids) where X : IStorable;
        public List<X> RemoveRecordsItems<X>(List<X> items) where X : IStorable;
    }

    public class SimpleDatabaseDM<U> : DatabaseDM<SimpleDatabaseDM<U>, U> where U : IStorable
    {
    }
    public class DatabaseDM<T, U> : GenericDatabaseDM<T, U> where T : IGenericDM<U>, new() where U : IStorable
    {
        public override sealed Task<VoidWithError> SetConfiguration(PyramidInfo pyramid, DataManagerConfig config)
        {
            return base.SetConfiguration(pyramid, config);
        }

        public override sealed IDeleteBuilder<X> CreateDelete<X>()
        {
            return base.CreateDelete<X>();
        }

        public override sealed IQueryBuilder<X> CreateQuery<X>()
        {
            return base.CreateQuery<X>();
        }
        public override sealed IUpdateBuilder<X> CreateUpdate<X>()
        {
            return base.CreateUpdate<X>();
        }
        protected override sealed ResultWithError<List<X>> CreateLogic<X>(List<X> values)
        {
            return base.CreateLogic(values);
        }

        protected override sealed ResultWithError<List<X>> DeleteLogic<X>(List<X> values)
        {
            return base.DeleteLogic(values);
        }

        protected override sealed ResultWithError<List<X>> GetAllLogic<X>()
        {
            return base.GetAllLogic<X>();
        }
        protected override sealed ResultWithError<List<X>> GetByIdsLogic<X>(List<int> ids)
        {
            return base.GetByIdsLogic<X>(ids);
        }
        protected override sealed ResultWithError<X> GetByIdLogic<X>(int id)
        {
            return base.GetByIdLogic<X>(id);
        }

        protected override sealed ResultWithError<List<X>> UpdateLogic<X>(List<X> values)
        {
            return base.UpdateLogic(values);
        }

        protected override sealed ResultWithError<List<X>> WhereLogic<X>(Expression<Func<X, bool>> func)
        {
            return base.WhereLogic(func);
        }
    }
    public class GenericDatabaseDM<T, U> : GenericDM<T, U>, IDatabaseDM where T : IGenericDM<U>, new() where U : IStorable
    {
         
        private readonly Dictionary<int, U> Records = new Dictionary<int, U>();

        public bool NeedLocalCache { get; private set; } = false;
        public bool NeedShortLink { get; private set; } = false;
        public List<string>? ShortLinks { get; private set; } = null;

        private IDBStorage? storage;
        protected List<Type> GetAllDone { get; } = new List<Type>();


        public IDBStorage Storage
        {
            get
            {
                if (storage != null)
                {
                    return storage;
                }
                throw new DataError(DataErrorCode.StorageNotFound, "You must define a storage inside your DM " + GetType().Name).GetException();
            }
        }

        #region Config
        protected virtual IDBStorage? DefineStorage()
        {
            return null;
        }
        protected virtual IDBStorage? SearchAttributeStorage()
        {
            Attributes.Storage? attr = typeof(U).GetCustomAttribute<Attributes.Storage>();
            if(attr != null) {
                Type type = attr.type;
                if(!DBStorage.listStorage.ContainsKey(type))
                {
                    DBStorage.listStorage.Add(type, (IDBStorage)TypeTools.CreateNewObj(type));
                }
                return DBStorage.listStorage[type];
            }
            return null;
        }
        protected virtual bool? UseLocalCache()
        {
            return null;
        }
        protected virtual bool? UseShortLink()
        {
            return null;
        }
        protected void ShortLink<X>(Expression<Func<X, IStorable>> fct) where X : U
        {
            ShortLinks ??= new List<string>();
            ShortLinks.Add(LambdaToPath.Translate(fct));
        }
        /// <summary>
        /// Call the metho ShortLink to define which table will be in shortlink (only id inside object)
        /// </summary>
        /// <typeparam name="X"></typeparam>
        protected virtual void DefineShortLinks<X>() where X : U
        {
        }

        public bool IsShortLink(string path)
        {
            if (ShortLinks == null)
            {
                return NeedShortLink;
            }
            return ShortLinks.Contains(path);
        }
        public override async Task<VoidWithError> SetConfiguration(PyramidInfo pyramid, DataManagerConfig config)
        {
            VoidWithError result = new VoidWithError();
            storage = DefineStorage();
            storage ??= SearchAttributeStorage();
            storage ??= config.defaultStorage;
            if (storage == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.StorageNotFound, "Can't found a storage for " + Name));
                return result;
            }
            bool? localCacheTemp = UseLocalCache();
            if (localCacheTemp == null)
            {
                NeedLocalCache = config.preferLocalCache;
            }
            else
            {
                NeedLocalCache = (bool)localCacheTemp;
            }
            DefineShortLinks<U>();
            bool? shortLinkTemp = UseShortLink();
            if (shortLinkTemp == null)
            {
                NeedShortLink = config.preferShortLink;
            }
            else
            {
                NeedShortLink = (bool)shortLinkTemp;
            }
            if (!storage.IsConnectedOneTime)
            {
                VoidWithError resultTemp = storage.ConnectWithError();
                if (!resultTemp.Success)
                {
                    return resultTemp;
                }
            }
            storage.AddPyramid(pyramid);
            return await base.SetConfiguration(pyramid, config);

        }
        protected override Task<VoidWithError> Initialize()
        {
            VoidWithError result = new VoidWithError();
            if (storage != null)
            {
                result = storage.CreateLinks();
                if (!result.Success) return Task.FromResult(result);
                if (Config != null && Config.AutoCreateModel)
                {
                    result = storage.CreateTable(PyramidInfo);
                }
                return Task.FromResult(result);
            }
            result.Errors.Add(new DataError(DataErrorCode.StorageNotFound, "You must define a storage inside your DM " + GetType().Name));
            return Task.FromResult(result);
        }
        internal override IMigrationProvider GetMigrationProvider()
        {
            return Storage.GetMigrationProvider();
        }


        #endregion

        #region Get
        public override IQueryBuilder<X> CreateQuery<X>()
        {
            return new DatabaseQueryBuilder<X>(Storage, this) { UseShortObject = false };
        }

        // private readonly Dictionary<Type, object> savedGetAllQuery = new();
        protected override ResultWithError<List<X>> GetAllLogic<X>()
        {
            if (NeedLocalCache)
            {
                return GetAllWithErrorCache<X>();
            }
            return GetAllWithErrorNoCache<X>();
        }
        protected ResultWithError<List<X>> GetAllWithErrorCache<X>() where X : U
        {
            Type type = typeof(X);
            Type rootType = typeof(U);
            if (GetAllDone.Contains(rootType) || GetAllDone.Contains(type))
            {
                ResultWithError<List<X>> result = new()
                {
                    Result = new List<X>()
                };
                foreach (KeyValuePair<int, U> record in Records)
                {
                    if (record is X casted)
                    {
                        result.Result.Add(casted);
                    }
                }
                return result;
            }
            ResultWithError<List<X>> resultNoCache = GetAllWithErrorNoCache<X>();
            if (resultNoCache.Success && resultNoCache.Result != null)
            {
                List<X> finalResult = new List<X>();
                foreach (X newRecord in resultNoCache.Result)
                {
                    if (!Records.ContainsKey(newRecord.Id))
                    {
                        finalResult.Add(newRecord);
                        Records.Add(newRecord.Id, newRecord);
                    }
                    else
                    {
                        U item = Records[newRecord.Id];
                        if (item is X casted)
                        {
                            finalResult.Add(casted);
                        }
                    }
                }
                resultNoCache.Result = finalResult;
                GetAllDone.Add(type);
            }
            return resultNoCache;
        }
        protected ResultWithError<List<X>> GetAllWithErrorNoCache<X>() where X : U
        {
            return new DatabaseQueryBuilder<X>(Storage, this).RunWithError();
            // Type x = typeof(X);
            // if (!savedGetAllQuery.ContainsKey(x))
            // {
            //     savedGetAllQuery[x] = new DatabaseQueryBuilder<X>(Storage, this);
            // }
            // return ((DatabaseQueryBuilder<X>)savedGetAllQuery[x]).RunWithError();
        }


        // private readonly Dictionary<Type, object> savedGetByIdQuery = new();
        // private readonly Mutex savedGetByIdQueryMutex = new();
        protected override ResultWithError<X> GetByIdLogic<X>(int id)
        {
            if (NeedLocalCache)
            {
                return GetByIdWithErrorCache<X>(id);
            }
            return GetByIdWithErrorNoCache<X>(id);
        }

        public ResultWithError<X> GetByIdWithErrorCache<X>(int id) where X : U
        {
            if (Records.ContainsKey(id) && Records[id] is X casted)
            {
                ResultWithError<X> result = new()
                {
                    Result = casted
                };
                return result;
            }
            ResultWithError<X> resultNoCache = GetByIdWithErrorNoCache<X>(id);
            if (resultNoCache.Success && resultNoCache.Result != null)
            {
                Records[resultNoCache.Result.Id] = resultNoCache.Result;
            }
            return resultNoCache;
        }
        public ResultWithError<X> GetByIdWithErrorNoCache<X>(int id) where X : U
        {
            ResultWithError<X> result = new();

            // Type x = typeof(X);
            // savedGetByIdQueryMutex.WaitOne();
            // if (!savedGetByIdQuery.ContainsKey(x))
            // {
            //     DatabaseQueryBuilder<X> queryBuilderTemp = new(Storage, this);
            //     queryBuilderTemp.WhereWithParameters(i => i.Id == id);
            //     savedGetByIdQuery[x] = queryBuilderTemp;
            // }
            // DatabaseQueryBuilder<X> queryBuilder = (DatabaseQueryBuilder<X>)savedGetByIdQuery[x];
            // queryBuilder.SetVariable("id", id);
            // ResultWithError<List<X>> resultTemp = queryBuilder.RunWithError();
            // savedGetByIdQueryMutex.ReleaseMutex();
            ResultWithError<List<X>> resultTemp = new DatabaseQueryBuilder<X>(Storage, this)
                                                                                    .Where(i => i.Id == id)
                                                                                    .RunWithError();

            if (resultTemp.Success)
            {
                if (resultTemp.Result?.Count == 1)
                {
                    result.Result = resultTemp.Result[0];
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.ItemNoExistInsideStorage, "The item " + id + " can't be found inside the storage"));
                }
            }
            else
            {
                result.Errors.AddRange(resultTemp.Errors);
            }
            return result;
        }

        // private readonly Dictionary<Type, object> savedGetByIdsQuery = new();
        protected override ResultWithError<List<X>> GetByIdsLogic<X>(List<int> ids)
        {
            if (NeedLocalCache)
            {
                return GetByIdsWithErrorCache<X>(ids);
            }
            return GetByIdsWithErrorNoCache<X>(ids);
        }

        public ResultWithError<List<X>> GetByIdsWithErrorCache<X>(List<int> ids) where X : U
        {
            ResultWithError<List<X>> result = new()
            {
                Result = new List<X>()
            };
            List<int> missingIds = new();
            foreach (int id in ids)
            {
                if (Records.ContainsKey(id))
                {
                    if (Records[id] is X casted)
                    {
                        result.Result.Add(casted);
                    }
                }
                else
                {
                    missingIds.Add(id);
                }
            }
            if (missingIds.Count > 0)
            {
                ResultWithError<List<X>> resultNoCache = GetByIdsWithErrorNoCache<X>(missingIds);
                if (resultNoCache.Success && resultNoCache.Result != null)
                {
                    foreach (X item in resultNoCache.Result)
                    {
                        if (!Records.ContainsKey(item.Id))
                        {
                            Records.Add(item.Id, item);
                        }
                        result.Result.Add(item);
                    }
                }
                else
                {
                    result.Result.Clear();
                    result.Errors.AddRange(resultNoCache.Errors);
                }
            }
            return result;
        }
        public ResultWithError<List<X>> GetByIdsWithErrorNoCache<X>(List<int> ids) where X : U
        {
            // Type x = typeof(X);
            // if (!savedGetByIdsQuery.ContainsKey(x))
            // {
            //     DatabaseQueryBuilder<X> queryBuilderTemp = new(Storage, this);
            //     queryBuilderTemp.WhereWithParameters(i => ids.Contains(i.Id));
            //     savedGetByIdsQuery[x] = queryBuilderTemp;
            // }
            // DatabaseQueryBuilder<X> queryBuilder = (DatabaseQueryBuilder<X>)savedGetByIdsQuery[x];
            // queryBuilder.SetVariable("ids", ids);
            // ResultWithError<List<X>> resultTemp = queryBuilder.RunWithError();
            // return resultTemp;

            return new DatabaseQueryBuilder<X>(Storage, this).Where(i => ids.Contains(i.Id)).RunWithError();
        }


        protected override ResultWithError<List<X>> WhereLogic<X>(Expression<Func<X, bool>> func)
        {
            if (NeedLocalCache)
            {
                return WhereWithErrorCache(func);
            }
            return WhereWithErrorNoCache(func);
        }

        public ResultWithError<List<X>> WhereWithErrorCache<X>(Expression<Func<X, bool>> func) where X : U
        {
            Type type = typeof(X);
            Type rootType = typeof(U);
            if (GetAllDone.Contains(rootType) || GetAllDone.Contains(type))
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
                return result;
            }
            return WhereWithErrorNoCache(func);
        }

        public ResultWithError<List<X>> WhereWithErrorNoCache<X>(Expression<Func<X, bool>> func) where X : U
        {
            DatabaseQueryBuilder<X> queryBuilder = new(Storage, this);
            queryBuilder.Where(func);
            return queryBuilder.RunWithError();
        }

        #endregion

        #region Exist
        public override IExistBuilder<X> CreateExist<X>()
        {
            return new DatabaseExistBuilder<X>(Storage, this);
        }

        #endregion

        #region Create

        // private readonly Dictionary<Type, object> savedCreateQuery = new();
        protected override ResultWithError<List<X>> CreateLogic<X>(List<X> values)
        {
            return RunInsideTransaction(new List<X>(), delegate ()
            {
                ResultWithError<List<X>> result = new()
                {
                    Result = new List<X>()
                };

                foreach (X value in values)
                {
                    // Type type = value.GetType();
                    // if (!savedCreateQuery.ContainsKey(type))
                    // {
                    //     DatabaseCreateBuilder<X> query = new(Storage, this, type);
                    //     savedCreateQuery[type] = query;
                    // }

                    // ResultWithError<X> resultTemp = ((DatabaseCreateBuilder<X>)savedCreateQuery[type]).RunWithError(value);
                    ResultWithError<X> resultTemp = new DatabaseCreateBuilder<X>(Storage, this, value.GetType()).RunWithError(value);
                    if (resultTemp.Success && resultTemp.Result != null)
                    {
                        if (NeedLocalCache)
                        {
                            Records[resultTemp.Result.Id] = resultTemp.Result;
                        }
                        result.Result.Add(resultTemp.Result);
                    }
                    else
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                    }
                }

                return result;
            });
        }

        protected override VoidWithError BulkCreateLogic<X>(List<X> values, bool withId)
        {
            return RunInsideTransaction(delegate ()
            {
                VoidWithError result = new();
                try
                {
                    if (values.Count == 0) return new();
                    X value = values[0];
                    DatabaseCreateBuilder<X> builder = new DatabaseCreateBuilder<X>(Storage, this, value.GetType());
                    result = builder.RunBulkWithError(values, withId);
                }
                catch (Exception e)
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            });
        }


        #endregion

        #region Update
        public override IUpdateBuilder<X> CreateUpdate<X>()
        {
            return new DatabaseUpdateBuilder<X>(Storage, this, NeedLocalCache);
        }
        // private readonly Dictionary<Type, object> savedUpdateQuery = new();
        protected override ResultWithError<List<X>> UpdateLogic<X>(List<X> values)
        {
            return RunInsideTransaction(new List<X>(), delegate ()
            {
                ResultWithError<List<X>> result = new()
                {
                    Result = new List<X>()
                };
                int id = 0;
                foreach (X value in values)
                {
                    // Type type = value.GetType();
                    // if (!savedUpdateQuery.ContainsKey(type))
                    // {
                    //     DatabaseUpdateBuilder<X> query = new(Storage, this, NeedLocalCache, type);
                    //     query.WhereWithParameters(p => p.Id == id);
                    //     savedUpdateQuery[type] = query;
                    // }

                    // ResultWithError<X> resultTemp = ((DatabaseUpdateBuilder<X>)savedUpdateQuery[type]).Prepare(value.Id).SingleWithError(value);
                    id = value.Id;
                    ResultWithError<X> resultTemp = new DatabaseUpdateBuilder<X>(Storage, this, NeedLocalCache, value.GetType())
                                                            .Where(p => p.Id == id)
                                                            .SingleWithError(value);

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
            });
        }
        #endregion

        #region Delete
        public override IDeleteBuilder<X> CreateDelete<X>()
        {
            return new DatabaseDeleteBuilder<X>(Storage, this, NeedLocalCache);
        }
        // private readonly Dictionary<Type, object> savedDeleteQuery = new();
        protected override ResultWithError<List<X>> DeleteLogic<X>(List<X> values)
        {
            return RunInsideTransaction(new List<X>(), delegate ()
            {
                ResultWithError<List<X>> result = new()
                {
                    Result = new List<X>()
                };
                int id = 0;
                foreach (X value in values)
                {
                    // Type type = value.GetType();
                    // if (!savedDeleteQuery.ContainsKey(type))
                    // {
                    //     DatabaseDeleteBuilder<X> query = new(Storage, this, NeedLocalCache, type);
                    //     query.WhereWithParameters(p => p.Id == id);
                    //     savedDeleteQuery[type] = query;
                    // }

                    // ResultWithError<List<X>> resultTemp = ((DatabaseDeleteBuilder<X>)savedDeleteQuery[type]).Prepare(value.Id).RunWithError();
                    id = value.Id;
                    ResultWithError<List<X>> resultTemp = new DatabaseDeleteBuilder<X>(Storage, this, NeedLocalCache, value.GetType())
                                                                .Where(p => p.Id == id)
                                                                .RunWithError();
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
            });
        }

        public List<X> RemoveRecordsItems<X>(List<int> ids) where X : IStorable
        {
            List<X> result = new();
            foreach (int id in ids)
            {
                if (Records.ContainsKey(id) && Records[id] is X casted)
                {
                    result.Add(casted);
                    Records.Remove(id);
                }
            }
            return result;
        }
        public List<X> RemoveRecordsItems<X>(List<X> items) where X : IStorable
        {
            List<X> result = new();
            foreach (X item in items)
            {
                if (item is U && Records.ContainsKey(item.Id) && Records[item.Id] is X casted)
                {
                    result.Add(casted);
                    Records.Remove(item.Id);
                }
            }
            return result;
        }


        #endregion

        #region Transaction
        protected override SemaphoreSlim getTransactionLocker()
        {
            return Storage.getTransactionLocker();
        }
        protected override TransactionContext? getTransactionContext()
        {
            return Storage.getTransactionContext();
        }
        protected override void setTransactionContext(TransactionContext? context)
        {
            Storage.setTransactionContext(context);
        }
        protected override ResultWithError<TransactionContext> BeginTransactionContext()
        {
            ResultWithError<TransactionContext> result = new();
            DbConnection connection = Storage.GetConnection();
            try
            {
                connection.Open();
            }
            catch
            {
                result.Errors.Add(new DataError(DataErrorCode.StorageDisconnected, "The storage " + GetType().Name + "(" + ToString() + ") can't connect to the database"));
                return result;
            }
            DbTransaction transaction = connection.BeginTransaction();
            result.Result = new DbTransactionContext(transaction, EndTransaction, RunInsideLocker);
            return result;
        }
        protected override void EndTransactionContext()
        {
            if (getTransactionContext() is DbTransactionContext dbTransactionContext)
            {
                dbTransactionContext.transaction.Dispose();
                if (dbTransactionContext.transaction.Connection != null)
                {
                    dbTransactionContext.transaction.Connection.Dispose();
                }
            }
        }
        #endregion
    }
}
