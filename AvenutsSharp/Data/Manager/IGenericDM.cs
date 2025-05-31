using AventusSharp.Data.Migrations;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager
{
    public delegate void OnCreatedHandler<U>(ResultWithError<List<U>> result);
    public delegate void OnUpdatedHandler<U>(ResultWithError<List<U>> result);
    public delegate void OnDeletedHandler<U>(ResultWithError<List<U>> result);

    public interface IGenericDM
    {
        Type GetMainType();
        List<Type> DefineManualDependances();
        string Name { get; }
        bool IsInit { get; }

        Task<VoidWithError> SetConfiguration(PyramidInfo pyramid, DataManagerConfig config);
        Task<VoidWithError> Init();

        #region Get
        List<X> GetAll<X>() where X : notnull;
        ResultWithError<List<X>> GetAllWithError<X>() where X : notnull;
        IQueryBuilder<X> CreateQuery<X>() where X : notnull;
        IUpdateBuilder<X> CreateUpdate<X>() where X : notnull;
        IDeleteBuilder<X> CreateDelete<X>() where X : notnull;
        IExistBuilder<X> CreateExist<X>() where X : notnull;

        object? GetById(int id);
        X? GetById<X>(int id) where X : notnull;
        ResultWithError<X> GetByIdWithError<X>(int id) where X : notnull;

        List<X> GetByIds<X>(List<int> ids) where X : notnull;
        ResultWithError<List<X>> GetByIdsWithError<X>(List<int> ids) where X : notnull;

        List<X> Where<X>(Expression<Func<X, bool>> func) where X : notnull;
        ResultWithError<List<X>> WhereWithError<X>(Expression<Func<X, bool>> func) where X : notnull;

        bool Exist<X>(Expression<Func<X, bool>> func) where X : notnull;
        ResultWithError<bool> ExistWithError<X>(Expression<Func<X, bool>> func) where X : notnull;


        X? Single<X>(Expression<Func<X, bool>> func) where X : notnull;
        ResultWithError<X> SingleWithError<X>(Expression<Func<X, bool>> func) where X : notnull;
        #endregion

        #region Create
        List<X> Create<X>(List<X> values) where X : notnull, IStorable;
        ResultWithError<List<X>> CreateWithError<X>(List<X> values) where X : notnull, IStorable;
        X? Create<X>(X value) where X : notnull, IStorable;
        ResultWithError<X> CreateWithError<X>(X value) where X : notnull, IStorable;
        #endregion

        #region Update
        List<X> Update<X>(List<X> values) where X : notnull, IStorable;
        ResultWithError<List<X>> UpdateWithError<X>(List<X> values) where X : notnull, IStorable;
        X Update<X>(X value) where X : notnull, IStorable;
        ResultWithError<X> UpdateWithError<X>(X value) where X : notnull, IStorable;
        #endregion

        #region Delete
        List<X> Delete<X>(List<X> values) where X : notnull, IStorable;
        ResultWithError<List<X>> DeleteWithError<X>(List<X> values) where X : notnull, IStorable;
        X Delete<X>(X value) where X : notnull, IStorable;
        ResultWithError<X> DeleteWithError<X>(X value) where X : notnull, IStorable;
        #endregion

        void OnItemLoaded<X>(X item) where X : notnull, IStorable;

        internal void PrintErrors(IWithError withError);

        internal VoidWithError ApplyMigration<X>(IMigrationModel model) where X : notnull, IStorable;
    }
    public interface IGenericDM<U> : IGenericDM where U : notnull, IStorable
    {
        #region Get
        new List<X> GetAll<X>() where X : U;
        new ResultWithError<List<X>> GetAllWithError<X>() where X : U;
        new IQueryBuilder<X>? CreateQuery<X>() where X : U;
        new IUpdateBuilder<X>? CreateUpdate<X>() where X : U;

        new X? GetById<X>(int id) where X : U;
        new ResultWithError<X> GetByIdWithError<X>(int id) where X : U;

        new List<X>? GetByIds<X>(List<int> ids) where X : U;
        new ResultWithError<List<X>> GetByIdsWithError<X>(List<int> id) where X : U;

        new List<X> Where<X>(Expression<Func<X, bool>> func) where X : U;
        new ResultWithError<List<X>> WhereWithError<X>(Expression<Func<X, bool>> func) where X : U;

        new X? Single<X>(Expression<Func<X, bool>> func) where X : U;
        new ResultWithError<X> SingleWithError<X>(Expression<Func<X, bool>> func) where X : U;
        #endregion

        #region Create
        new List<X> Create<X>(List<X> values) where X : U;
        new ResultWithError<List<X>> CreateWithError<X>(List<X> values) where X : U;
        new X? Create<X>(X value) where X : U;
        new ResultWithError<X> CreateWithError<X>(X value) where X : U;

        event OnCreatedHandler<U> OnCreated;
        #endregion

        #region Update
        new List<X> Update<X>(List<X> values) where X : U;
        new ResultWithError<List<X>> UpdateWithError<X>(List<X> values) where X : U;
        new X? Update<X>(X value) where X : U;
        new ResultWithError<X> UpdateWithError<X>(X value) where X : U;

        event OnUpdatedHandler<U> OnUpdated;
        #endregion

        #region Delete
        new List<X> Delete<X>(List<X> values) where X : U;
        new ResultWithError<List<X>> DeleteWithError<X>(List<X> values) where X : U;
        new X? Delete<X>(X value) where X : U;
        new ResultWithError<X> DeleteWithError<X>(X value) where X : U;

        event OnDeletedHandler<U> OnDeleted;

        new void OnItemLoaded<X>(X item) where X : U;
        #endregion
    }
}
