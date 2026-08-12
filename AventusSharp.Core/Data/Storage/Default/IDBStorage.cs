using AventusSharp.Chart;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Migrations;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Storage.Default
{
    public interface IDBStorage
    {
        string? DateTimeFormat { get; }
        bool SupportsNativeBoolean => false;
        bool IsConnectedOneTime { get; }
        bool Debug { get; set; }

        DbConnection GetConnection();
        VoidWithError CreateLinks();
        VoidWithDataError AddPyramid(PyramidInfo pyramid);
        TableInfo? GetTableInfo(Type type);
        Task<ResultWithError<List<X>>> QueryFromBuilder<X>(DatabaseQueryBuilder<X> queryBuilder) where X : IStorable;
        Task<VoidWithError> QueryStreamFromBuilder<X>(DatabaseQueryBuilder<X> queryBuilder, Func<X, Task<VoidWithError>> action) where X : IStorable;
        Task<ResultWithError<bool>> ExistFromBuilder<X>(DatabaseExistBuilder<X> queryBuilder) where X : IStorable;
        Task<VoidWithError> BulkCreateFromBuilder<X>(DatabaseCreateBuilder<X> queryBuilder, List<X> items, bool withId) where X : IStorable;
        Task<VoidWithError> CreateFromBuilder<X>(DatabaseCreateBuilder<X> queryBuilder, X item) where X : IStorable;
        Task<ResultWithError<List<int>>> UpdateFromBuilder<X>(DatabaseUpdateBuilder<X> queryBuilder, X item) where X : IStorable;
        Task<VoidWithError> DeleteFromBuilder<X>(DatabaseDeleteBuilder<X> queryBuilder, List<X> elementsToDelete) where X : IStorable;
        Task<VoidWithError> CreateTable(PyramidInfo pyramid, bool force);
        Task<ResultWithError<bool>> TableExist(PyramidInfo pyramid);

        Task<bool> Connect();
        Task<VoidWithError> ConnectWithError();
        Task<ResultWithError<bool>> ResetStorage();
        Task<VoidWithError> IsConnected();
        Task Close();

        ResultWithDataError<DbCommand> CreateCmd(string sql);
        DbParameter GetDbParameter();

        string GetDatabaseName();
        ResultWithError<Dictionary<TableInfo, IList>> GroupDataByType<X>(IList data);

        // ResultWithError<Y> RunInsideTransaction<Y>(Y? defaultValue, Func<ResultWithError<Y>> action);
        // ResultWithError<Y> RunInsideTransaction<Y>(Func<ResultWithError<Y>> action);
        // VoidWithError RunInsideTransaction(Func<VoidWithError> action);

        TransactionContext? getTransactionScope();
        void setTransactionScope(TransactionContext? context);

        abstract IMigrationProvider GetMigrationProvider();

        void LoadAllTableFieldsQuery<X>(TableInfo tableInfo, string alias, DatabaseBuilderInfo baseInfo, List<string> path, List<Type> types, DatabaseGenericBuilder<X> queryBuilder) where X : IStorable;

        Task<VoidWithError> Execute(string sql, string callerPath = "", int callerNo = 0);
        Task<VoidWithError> Execute(DbCommand command, Dictionary<string, object?> parameters, string callerPath = "", int callerNo = 0);
        Task<VoidWithError> Execute(DbCommand command, List<Dictionary<string, object?>>? dataParameters, string callerPath = "", int callerNo = 0);
        Task<VoidWithError> ExecuteNoTransaction(string sql, string callerPath = "", int callerNo = 0);
        Task<VoidWithError> ExecuteNoTransaction(DbCommand command, Dictionary<string, object?> parameters, string callerPath = "", int callerNo = 0);
        Task<VoidWithError> ExecuteNoTransaction(DbCommand command, List<Dictionary<string, object?>>? dataParameters, string callerPath = "", int callerNo = 0);

        Task<ResultWithError<List<X>>> Query<X>(string sql, string callerPath = "", int callerNo = 0);
        Task<ResultWithError<List<X>>> Query<X>(DbCommand command, List<Dictionary<string, object?>>? dataParameters, string callerPath = "", int callerNo = 0);
        
        Task<ResultWithError<List<Dictionary<string, string?>>>> Query(string sql, string callerPath = "", int callerNo = 0);
        Task<ResultWithError<List<Dictionary<string, string?>>>> Query(DbCommand command, List<Dictionary<string, object?>>? dataParameters, string callerPath = "", int callerNo = 0);

        Task<VoidWithError> QueryStream(string sql, Func<Dictionary<string, string?>, Task<VoidWithError>> action, string callerPath = "", int callerNo = 0);
        Task<VoidWithError> QueryStream(DbCommand command, List<Dictionary<string, object?>>? dataParameters, Func<Dictionary<string, string?>, Task<VoidWithError>> action, string callerPath = "", int callerNo = 0);
        
        Task<ResultWithError<Y>> RunInsideTransaction<Y>(Y? defaultValue, Func<Task<ResultWithError<Y>>> action);
        Task<ResultWithError<Y>> RunInsideTransaction<Y>(Func<Task<ResultWithError<Y>>> action);
        Task<VoidWithError> RunInsideTransaction(Func<Task<VoidWithError>> action);

        List<DiagramObject> GetDiagrams(DiagramConfigInternal config);
    }


}
