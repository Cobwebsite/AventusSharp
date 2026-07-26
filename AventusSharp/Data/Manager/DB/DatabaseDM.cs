using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Collections.Concurrent;
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
        public bool IsSameStorage(IGenericDM? dm);
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
        protected override sealed Task<ResultWithError<List<X>>> CreateLogic<X>(List<X> values)
        {
            return base.CreateLogic(values);
        }

        protected override sealed Task<ResultWithError<List<X>>> DeleteLogic<X>(List<X> values)
        {
            return base.DeleteLogic(values);
        }

        protected override sealed Task<ResultWithError<List<X>>> GetAllLogic<X>()
        {
            return base.GetAllLogic<X>();
        }
        protected override sealed Task<ResultWithError<List<X>>> GetByIdsLogic<X>(List<int> ids)
        {
            return base.GetByIdsLogic<X>(ids);
        }
        protected override sealed Task<ResultWithError<X>> GetByIdLogic<X>(int id)
        {
            return base.GetByIdLogic<X>(id);
        }

        protected override sealed Task<ResultWithError<List<X>>> UpdateLogic<X>(List<X> values)
        {
            return base.UpdateLogic(values);
        }

        protected override sealed Task<ResultWithError<List<X>>> WhereLogic<X>(Expression<Func<X, bool>> func)
        {
            return base.WhereLogic(func);
        }
    }
    public class GenericDatabaseDM<T, U> : GenericDM<T, U>, IDatabaseDM where T : IGenericDM<U>, new() where U : IStorable
    {

        private readonly ConcurrentDictionary<int, U> Records = new();

        public bool NeedLocalCache { get; private set; } = false;
        public bool NeedShortLink { get; private set; } = false;
        public List<string>? ShortLinks { get; private set; } = null;

        private IDBStorage? storage;
        protected ConcurrentDictionary<Type, byte> GetAllDone { get; } = new();


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

        public bool IsSameStorage(IGenericDM? dm)
        {
            if (dm is IDatabaseDM databaseDM)
            {
                return databaseDM.Storage == Storage;
            }
            return false;
        }

        #region Config
        protected virtual IDBStorage? DefineStorage()
        {
            return null;
        }
        protected virtual IDBStorage? SearchAttributeStorage()
        {
            Attributes.Storage? attr = typeof(U).GetCustomAttribute<Attributes.Storage>();
            if (attr != null)
            {
                Type type = attr.type;
                if (!DBStorage.listStorage.ContainsKey(type))
                {
                    IDBStorage storage = (IDBStorage)TypeTools.CreateNewObj(type);
                    if (!DBStorage.listStorage.ContainsKey(type))
                    {
                        DBStorage.listStorage.Add(type, storage);
                    }
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
                VoidWithError resultTemp = await storage.ConnectWithError();
                if (!resultTemp.Success)
                {
                    return resultTemp;
                }
            }
            storage.AddPyramid(pyramid);
            return await base.SetConfiguration(pyramid, config);

        }
        protected override async Task<VoidWithError> Initialize()
        {
            VoidWithError result = new VoidWithError();
            if (storage != null)
            {
                result = storage.CreateLinks();
                if (!result.Success) return result;
                bool force = Config != null && Config.AutoCreateModel;
                result = await storage.CreateTable(PyramidInfo, force);

                return result;
            }
            result.Errors.Add(new DataError(DataErrorCode.StorageNotFound, "You must define a storage inside your DM " + GetType().Name));
            return result;
        }
        internal override IMigrationProvider GetMigrationProvider()
        {
            return Storage.GetMigrationProvider();
        }
        #endregion

        #region Get
        public override Task OnItemLoaded<X>(X item)
        {
            if (NeedLocalCache && item.Id > 0 && item is U cachedItem)
            {
                Records.GetOrAdd(item.Id, cachedItem);
            }
            return Task.CompletedTask;
        }

        public override IQueryBuilder<X> CreateQuery<X>()
        {
            return new DatabaseQueryBuilder<X>(Storage, this) { UseShortObject = false };
        }

        // private readonly Dictionary<Type, object> savedGetAllQuery = new();
        protected override Task<ResultWithError<List<X>>> GetAllLogic<X>()
        {
            if (NeedLocalCache)
            {
                return GetAllWithErrorCache<X>();
            }
            return GetAllWithErrorNoCache<X>();
        }
        protected async Task<ResultWithError<List<X>>> GetAllWithErrorCache<X>() where X : U
        {
            Type type = typeof(X);
            Type rootType = typeof(U);
            if (GetAllDone.ContainsKey(rootType) || GetAllDone.ContainsKey(type))
            {
                ResultWithError<List<X>> result = new()
                {
                    Result = new List<X>()
                };
                foreach (KeyValuePair<int, U> record in Records)
                {
                    if (record.Value is X casted)
                    {
                        result.Result.Add(casted);
                    }
                }
                return result;
            }
            ResultWithError<List<X>> resultNoCache = await GetAllWithErrorNoCache<X>();
            if (resultNoCache.Success && resultNoCache.Result != null)
            {
                List<X> finalResult = new List<X>();
                foreach (X newRecord in resultNoCache.Result)
                {
                    U canonical = Records.GetOrAdd(newRecord.Id, newRecord);
                    if (canonical is X casted)
                    {
                        finalResult.Add(casted);
                    }
                }
                resultNoCache.Result = finalResult;
                GetAllDone.TryAdd(type, 0);
            }
            return resultNoCache;
        }
        protected Task<ResultWithError<List<X>>> GetAllWithErrorNoCache<X>() where X : U
        {
            return new DatabaseQueryBuilder<X>(Storage, this).RunWithError();
        }

        protected override Task<ResultWithError<X>> GetByIdLogic<X>(int id)
        {
            if (NeedLocalCache)
            {
                return GetByIdWithErrorCache<X>(id);
            }
            return GetByIdWithErrorNoCache<X>(id);
        }

        public async Task<ResultWithError<X>> GetByIdWithErrorCache<X>(int id) where X : U
        {
            if (Records.TryGetValue(id, out U? existing) && existing is X casted)
            {
                ResultWithError<X> result = new()
                {
                    Result = casted
                };
                return result;
            }
            ResultWithError<X> resultNoCache = await GetByIdWithErrorNoCache<X>(id);
            if (resultNoCache.Success && resultNoCache.Result != null)
            {
                U canonical = Records.GetOrAdd(
                    resultNoCache.Result.Id,
                    resultNoCache.Result);
                if (canonical is X canonicalResult)
                {
                    resultNoCache.Result = canonicalResult;
                }
            }
            return resultNoCache;
        }
        public async Task<ResultWithError<X>> GetByIdWithErrorNoCache<X>(int id) where X : U
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
            ResultWithError<List<X>> resultTemp = await new DatabaseQueryBuilder<X>(Storage, this)
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
        protected override Task<ResultWithError<List<X>>> GetByIdsLogic<X>(List<int> ids)
        {
            if (NeedLocalCache)
            {
                return GetByIdsWithErrorCache<X>(ids);
            }
            return GetByIdsWithErrorNoCache<X>(ids);
        }

        public async Task<ResultWithError<List<X>>> GetByIdsWithErrorCache<X>(List<int> ids) where X : U
        {
            ResultWithError<List<X>> result = new()
            {
                Result = new List<X>()
            };
            List<int> missingIds = new();
            foreach (int id in ids)
            {
                if (Records.TryGetValue(id, out U? cached))
                {
                    if (cached is X casted)
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
                ResultWithError<List<X>> resultNoCache = await GetByIdsWithErrorNoCache<X>(missingIds);
                if (resultNoCache.Success && resultNoCache.Result != null)
                {
                    foreach (X item in resultNoCache.Result)
                    {
                        U canonical = Records.GetOrAdd(item.Id, item);
                        if (canonical is X casted)
                        {
                            result.Result.Add(casted);
                        }
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
        public Task<ResultWithError<List<X>>> GetByIdsWithErrorNoCache<X>(List<int> ids) where X : U
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


        protected override Task<ResultWithError<List<X>>> WhereLogic<X>(Expression<Func<X, bool>> func)
        {
            if (NeedLocalCache)
            {
                return WhereWithErrorCache(func);
            }
            return WhereWithErrorNoCache(func);
        }

        public async Task<ResultWithError<List<X>>> WhereWithErrorCache<X>(Expression<Func<X, bool>> func) where X : U
        {
            ResultWithError<List<X>> allRecords = await GetAllWithErrorCache<X>();
            ResultWithError<List<X>> result = new()
            {
                Result = new List<X>(),
                Errors = allRecords.Errors.ToList()
            };
            if (!allRecords.Success || allRecords.Result == null)
            {
                return result;
            }

            try
            {
                Func<X, bool> filter = func.Compile();
                foreach (X record in allRecords.Result)
                {
                    try
                    {
                        if (filter(record))
                        {
                            result.Result.Add(record);
                        }
                    }
                    catch (Exception e)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                    }
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }

            return result;
        }

        public Task<ResultWithError<List<X>>> WhereWithErrorNoCache<X>(Expression<Func<X, bool>> func) where X : U
        {
            DatabaseQueryBuilder<X> queryBuilder = new(Storage, this);
            queryBuilder.Where(func);
            return queryBuilder.RunWithError();
        }

        #endregion

        #region Exist
        protected override async Task<ResultWithError<bool>> ExistLogic<X>(
            Expression<Func<X, bool>> func)
        {
            if (!NeedLocalCache)
            {
                return await base.ExistLogic(func);
            }

            ResultWithError<List<X>> records = await WhereWithErrorCache(func);
            return new ResultWithError<bool>
            {
                Result = records.Success && records.Result is { Count: > 0 },
                Errors = records.Errors.ToList()
            };
        }

        public override IExistBuilder<X> CreateExist<X>()
        {
            return new DatabaseExistBuilder<X>(Storage, this);
        }

        #endregion

        #region Create
        public override ICreateBuilder<X> CreateCreate<X>()
        {
            return new DatabaseCreateBuilder<X>(Storage, this);
        }
        // private readonly Dictionary<Type, object> savedCreateQuery = new();
        protected override Task<ResultWithError<List<X>>> CreateLogic<X>(List<X> values)
        {
            return RunInsideTransaction(new List<X>(), async delegate ()
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
                    ResultWithError<X> resultTemp = await new DatabaseCreateBuilder<X>(Storage, this, value.GetType()).RunWithError(value);
                    if (resultTemp.Success && resultTemp.Result != null)
                    {
                        if (NeedLocalCache)
                        {
                            Records[resultTemp.Result.Id] = resultTemp.Result;
                            int createdId = resultTemp.Result.Id;
                            U createdItem = resultTemp.Result;
                            getTransactionScope()?.OnRollback(() =>
                            {
                                if (Records.TryGetValue(createdId, out U? cached)
                                    && ReferenceEquals(cached, createdItem))
                                {
                                    Records.TryRemove(createdId, out _);
                                }
                                return Task.FromResult(new VoidWithError());
                            });
                        }
                        result.Result.Add(resultTemp.Result);
                    }
                    else
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                        break;
                    }
                }

                return result;
            });
        }

        protected override Task<VoidWithError> BulkCreateLogic<X>(List<X> values, bool withId)
        {
            return RunInsideTransaction(async delegate ()
            {
                VoidWithError result = new();
                try
                {
                    if (values.Count == 0) return new();
                    X value = values[0];
                    DatabaseCreateBuilder<X> builder = new DatabaseCreateBuilder<X>(Storage, this, value.GetType());
                    result = await builder.RunBulkWithError(values, withId);
                    if (result.Success && NeedLocalCache)
                    {
                        // Bulk insertion does not necessarily return the generated ids.
                        // Invalidate the "complete" marker so the next cached read
                        // reloads the rows and registers them with their database ids.
                        GetAllDone.TryRemove(typeof(X), out _);
                        GetAllDone.TryRemove(typeof(U), out _);
                    }
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
        protected override Task<ResultWithError<List<X>>> UpdateLogic<X>(List<X> values)
        {
            return RunInsideTransaction(new List<X>(), async delegate ()
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
                    X? valueBeforeUpdate = default;
                    if (NeedLocalCache && getTransactionScope() != null)
                    {
                        ResultWithError<X> snapshot = await GetByIdWithErrorNoCache<X>(id);
                        if (!snapshot.Success || snapshot.Result == null)
                        {
                            result.Errors.AddRange(snapshot.Errors);
                            break;
                        }
                        valueBeforeUpdate = snapshot.Result;
                    }
                    ResultWithError<X> resultTemp = await new DatabaseUpdateBuilder<X>(Storage, this, NeedLocalCache, value.GetType())
                                                            .Where(p => p.Id == id)
                                                            .SingleWithError(value);

                    if (resultTemp.Success && resultTemp.Result != null)
                    {
                        if (valueBeforeUpdate != null)
                        {
                            X snapshot = valueBeforeUpdate;
                            getTransactionScope()?.OnRollback(async () =>
                            {
                                await RestorePersistentValues(value, snapshot);
                                return new VoidWithError();
                            });
                        }
                        result.Result.Add(resultTemp.Result);
                    }
                    else
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                        if (valueBeforeUpdate != null)
                        {
                            await RestorePersistentValues(value, valueBeforeUpdate);
                        }
                        break;
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
        protected override Task<ResultWithError<List<X>>> DeleteLogic<X>(List<X> values)
        {
            return RunInsideTransaction(new List<X>(), async delegate ()
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
                    ResultWithError<List<X>> resultTemp = await new DatabaseDeleteBuilder<X>(Storage, this, NeedLocalCache, value.GetType())
                                                                .Where(p => p.Id == id)
                                                                .RunWithError();
                    if (resultTemp.Success && resultTemp.Result?.Count > 0)
                    {
                        result.Result.Add(resultTemp.Result[0]);
                    }
                    else
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                        break;
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
                if (Records.TryGetValue(id, out U? cached) && cached is X casted)
                {
                    result.Add(casted);
                    Records.TryRemove(id, out _);
                    getTransactionScope()?.OnRollback(() =>
                    {
                        if (casted is U item)
                        {
                            Records[id] = item;
                        }
                        return Task.FromResult(new VoidWithError());
                    });
                }
            }
            return result;
        }
        public List<X> RemoveRecordsItems<X>(List<X> items) where X : IStorable
        {
            List<X> result = new();
            foreach (X item in items)
            {
                if (item is U
                    && Records.TryGetValue(item.Id, out U? cachedItem)
                    && cachedItem is X casted)
                {
                    result.Add(casted);
                    Records.TryRemove(item.Id, out _);
                    int id = item.Id;
                    getTransactionScope()?.OnRollback(() =>
                    {
                        if (casted is U cached)
                        {
                            Records[id] = cached;
                        }
                        return Task.FromResult(new VoidWithError());
                    });
                }
            }
            return result;
        }

        private async Task RestorePersistentValues<X>(X target, X snapshot) where X : IStorable
        {
            TableInfo? table = Storage.GetTableInfo(target.GetType());
            while (table != null)
            {
                foreach (TableMemberInfoSql member in table.Members)
                {
                    object? value = member.GetValue(snapshot);
                    if (value is IStorable storable && storable.Id > 0)
                    {
                        object? canonical = await GenericDM
                            .Get(storable.GetType())
                            .GetById(storable.Id);
                        value = canonical ?? value;
                    }
                    member.SetValue(target, value);
                }
                table = table.Parent;
            }
        }


        #endregion

        #region Transaction

        protected override TransactionContext? getTransactionScope()
        {
            return Storage.getTransactionScope();
        }
        protected override void setTransactionScope(TransactionContext? context)
        {
            Storage.setTransactionScope(context);
        }
        protected override async Task<ResultWithError<TransactionContext>> BeginTransactionScope()
        {
            ResultWithError<TransactionContext> result = new();
            DbConnection connection = Storage.GetConnection();
            try
            {
                await connection.OpenAsync();
            }
            catch
            {
                result.Errors.Add(new DataError(DataErrorCode.StorageDisconnected, "The storage " + GetType().Name + "(" + ToString() + ") can't connect to the database"));
                return result;
            }
            DbTransaction transaction = await connection.BeginTransactionAsync();
            result.Result = new DbTransactionContext(transaction, EndTransaction);
            return result;
        }
        protected override async Task EndTransactionScope()
        {
            if (getTransactionScope() is DbTransactionContext dbTransactionContext)
            {
                DbConnection connection = dbTransactionContext.Connection;
                await dbTransactionContext.transaction.DisposeAsync();
                await connection.DisposeAsync();
            }
        }
        #endregion
    }
}
