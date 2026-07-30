using AventusSharp.Data.Migrations;
using AventusSharp.Tools;
using Org.BouncyCastle.Math.EC.Rfc7748;
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
        ResultWithError<PyramidInfo> GetPyramidsInfo<X>();
        ResultWithError<DataMemberInfo> GetMemberInfo<X>(string name);
        ResultWithError<List<DataMemberInfo>> GetMembersInfo<X, Y>();
        List<Type> DefineManualDependencies();
        string Name { get; }
        bool IsInit { get; }

        Task<VoidWithError> SetConfiguration(PyramidInfo pyramid, DataManagerConfig config);
        Task<VoidWithError> Init();

        #region Get
        Task<List<X>> GetAll<X>() where X : notnull;
        Task<ResultWithError<List<X>>> GetAllWithError<X>() where X : notnull;
        IQueryBuilder<X> CreateQuery<X>() where X : notnull;
        ICreateBuilder<X> CreateCreate<X>() where X : notnull;
        IUpdateBuilder<X> CreateUpdate<X>() where X : notnull;
        IDeleteBuilder<X> CreateDelete<X>() where X : notnull;
        IExistBuilder<X> CreateExist<X>() where X : notnull;

        Task<object?> GetById(int id);
        Task<X?> GetById<X>(int id) where X : notnull;
        Task<ResultWithError<X>> GetByIdWithError<X>(int id) where X : notnull;

        Task<List<X>> GetByIds<X>(List<int> ids) where X : notnull;
        Task<ResultWithError<List<X>>> GetByIdsWithError<X>(List<int> ids) where X : notnull;

        Task<List<X>> Where<X>(Expression<Func<X, bool>> func) where X : notnull;
        Task<ResultWithError<List<X>>> WhereWithError<X>(Expression<Func<X, bool>> func) where X : notnull;

        Task<bool> Exist<X>(Expression<Func<X, bool>> func) where X : notnull;
        Task<ResultWithError<bool>> ExistWithError<X>(Expression<Func<X, bool>> func) where X : notnull;


        Task<X?> Single<X>(Expression<Func<X, bool>> func) where X : notnull;
        Task<ResultWithError<X>> SingleWithError<X>(Expression<Func<X, bool>> func) where X : notnull;
        #endregion

        #region Create
        Task<List<X>> Create<X>(List<X> values) where X : notnull, IStorable;
        Task<ResultWithError<List<X>>> CreateWithError<X>(List<X> values) where X : notnull, IStorable;
        Task<bool> BulkCreate<X>(List<X> values, bool withId = false) where X : notnull, IStorable;
        Task<VoidWithError> BulkCreateWithError<X>(List<X> values, bool withId = false) where X : notnull, IStorable;
        Task<X?> Create<X>(X value) where X : notnull, IStorable;
        Task<ResultWithError<X>> CreateWithError<X>(X value) where X : notnull, IStorable;

        #endregion

        #region Update
        Task<List<X>> Update<X>(List<X> values) where X : notnull, IStorable;
        Task<ResultWithError<List<X>>> UpdateWithError<X>(List<X> values) where X : notnull, IStorable;
        Task<X> Update<X>(X value) where X : notnull, IStorable;
        Task<ResultWithError<X>> UpdateWithError<X>(X value) where X : notnull, IStorable;
        #endregion

        #region Delete
        Task<List<X>> Delete<X>(List<X> values) where X : notnull, IStorable;
        Task<ResultWithError<List<X>>> DeleteWithError<X>(List<X> values) where X : notnull, IStorable;
        Task<X> Delete<X>(X value) where X : notnull, IStorable;
        Task<ResultWithError<X>> DeleteWithError<X>(X value) where X : notnull, IStorable;
        #endregion

        Task OnItemLoaded<X>(X item) where X : notnull, IStorable;

        internal void PrintErrors(IWithError withError);

        internal IMigrationProvider GetMigrationProvider();


        #region Transaction
        Task<ResultWithError<Y>> RunInsideTransaction<Y>(Y? defaultValue, Func<Task<ResultWithError<Y>>> action);
        Task<ResultWithError<Y>> RunInsideTransaction<Y>(Func<Task<ResultWithError<Y>>> action);
        Task<VoidWithError> RunInsideTransaction(Func<Task<VoidWithError>> action);

        #endregion
    }
    public interface IGenericDM<U> : IGenericDM where U : notnull, IStorable
    {
        new ResultWithError<PyramidInfo> GetPyramidsInfo<X>() where X : U;
        new ResultWithError<DataMemberInfo> GetMemberInfo<X>(string name) where X : U;
        new ResultWithError<List<DataMemberInfo>> GetMembersInfo<X, Y>() where X : U;

        #region Get
        new Task<List<X>> GetAll<X>() where X : U;
        new Task<ResultWithError<List<X>>> GetAllWithError<X>() where X : U;
        new IQueryBuilder<X>? CreateQuery<X>() where X : U;
        new IUpdateBuilder<X>? CreateUpdate<X>() where X : U;

        new Task<X?> GetById<X>(int id) where X : U;
        new Task<ResultWithError<X>> GetByIdWithError<X>(int id) where X : U;

        new Task<List<X>?> GetByIds<X>(List<int> ids) where X : U;
        new Task<ResultWithError<List<X>>> GetByIdsWithError<X>(List<int> id) where X : U;

        new Task<List<X>> Where<X>(Expression<Func<X, bool>> func) where X : U;
        new Task<ResultWithError<List<X>>> WhereWithError<X>(Expression<Func<X, bool>> func) where X : U;

        new Task<X?> Single<X>(Expression<Func<X, bool>> func) where X : U;
        new Task<ResultWithError<X>> SingleWithError<X>(Expression<Func<X, bool>> func) where X : U;
        #endregion

        #region Create
        new Task<List<X>> Create<X>(List<X> values) where X : U;
        new Task<ResultWithError<List<X>>> CreateWithError<X>(List<X> values) where X : U;
        new Task<X?> Create<X>(X value) where X : U;
        new Task<ResultWithError<X>> CreateWithError<X>(X value) where X : U;

        event OnCreatedHandler<U> OnCreated;
        #endregion

        #region Update
        new Task<List<X>> Update<X>(List<X> values) where X : U;
        new Task<ResultWithError<List<X>>> UpdateWithError<X>(List<X> values) where X : U;
        new Task<X?> Update<X>(X value) where X : U;
        new Task<ResultWithError<X>> UpdateWithError<X>(X value) where X : U;

        event OnUpdatedHandler<U> OnUpdated;
        #endregion

        #region Delete
        new Task<List<X>> Delete<X>(List<X> values) where X : U;
        new Task<ResultWithError<List<X>>> DeleteWithError<X>(List<X> values) where X : U;
        new Task<X?> Delete<X>(X value) where X : U;
        new Task<ResultWithError<X>> DeleteWithError<X>(X value) where X : U;

        event OnDeletedHandler<U> OnDeleted;

        new Task OnItemLoaded<X>(X item) where X : U;
        #endregion

    }
}
