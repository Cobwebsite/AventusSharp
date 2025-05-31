using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Migrations;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;

namespace AventusSharp.Data.Storage.Default
{
    public interface IDBStorage
    {
        public bool IsConnectedOneTime { get; }
        public bool Debug { get; set; }
        public VoidWithError CreateLinks();
        public VoidWithDataError AddPyramid(PyramidInfo pyramid);
        public TableInfo? GetTableInfo(Type type);
        public ResultWithError<List<X>> QueryFromBuilder<X>(DatabaseQueryBuilder<X> queryBuilder) where X : IStorable;
        public ResultWithError<bool> ExistFromBuilder<X>(DatabaseExistBuilder<X> queryBuilder) where X : IStorable;
        public VoidWithError CreateFromBuilder<X>(DatabaseCreateBuilder<X> queryBuilder, X item) where X : IStorable;
        public ResultWithError<List<int>> UpdateFromBuilder<X>(DatabaseUpdateBuilder<X> queryBuilder, X item) where X : IStorable;
        public VoidWithError DeleteFromBuilder<X>(DatabaseDeleteBuilder<X> queryBuilder, List<X> elementsToDelete) where X : IStorable;
        public VoidWithError CreateTable(PyramidInfo pyramid);
        public ResultWithError<bool> TableExist(PyramidInfo pyramid);

        public VoidWithError ConnectWithError();
        public ResultWithError<bool> ResetStorage();

        public string GetDatabaseName();
        public ResultWithError<Dictionary<TableInfo, IList>> GroupDataByType<X>(IList data);

        public ResultWithError<Y> RunInsideTransaction<Y>(Y? defaultValue, Func<ResultWithError<Y>> action);
        public ResultWithError<Y> RunInsideTransaction<Y>(Func<ResultWithError<Y>> action);
        public VoidWithError RunInsideTransaction(Func<VoidWithError> action);

        public abstract MigrationFactory GetMigrationProvider();
    }


}
