using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Migrations;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Routes.Request;
using AventusSharp.Tools;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI.Common;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager
{
    public static class GenericDM
    {
        private static readonly Dictionary<Type, IGenericDM> dico = new();
        private static readonly List<IGenericDM> dms = new();

        public static List<Type> GetExistingDMTypes()
        {
            return dms.Select(v => v.GetType()).ToList();
        }
        public static IGenericDM Get<U>() where U : IStorable
        {
            return Get(typeof(U));
        }
        public static IGenericDM Get(Type U)
        {
            if (dico.ContainsKey(U))
            {
                return dico[U];
            }
            throw new DataError(DataErrorCode.DMNotExist, "Can't found a data manger for type " + U.Name).GetException();
        }
        public static ResultWithError<IGenericDM> GetWithError<U>() where U : IStorable
        {
            return GetWithError(typeof(U));
        }
        public static ResultWithError<IGenericDM> GetWithError(Type U)
        {
            ResultWithError<IGenericDM> result = new ResultWithError<IGenericDM>();
            if (dico.ContainsKey(U))
            {
                result.Result = dico[U];
                return result;
            }
            result.Errors.Add(new DataError(DataErrorCode.DMNotExist, "Can't found a data manger for type " + U.Name));
            return result;
        }
        public static VoidWithDataError Set(Type type, IGenericDM manager)
        {
            VoidWithDataError result = new VoidWithDataError();
            if (dico.ContainsKey(type))
            {
                if (dico[type] != manager)
                {
                    result.Errors.Add(new DataError(DataErrorCode.DMAlreadyExist, "A manager already exists for type " + type.Name));
                }
            }
            else
            {
                if (!dms.Contains(manager))
                {
                    dms.Add(manager);
                }
                dico[type] = manager;
            }
            return result;

        }

    }
    public abstract class GenericDM<T, U> : IGenericDM<U> where T : IGenericDM<U>, new() where U : notnull, IStorable
    {
        #region singleton
        private static readonly Mutex mutexGetInstance = new();
        private static readonly Dictionary<Type, T> instances = new();
        ///// <summary>
        ///// Singleton pattern
        ///// </summary>
        ///// <returns></returns>
        public static T GetInstance()
        {
            mutexGetInstance.WaitOne();
            if (!instances.ContainsKey(typeof(T)))
            {
                T dm = new T();
                instances.Add(typeof(T), dm);
            }
            mutexGetInstance.ReleaseMutex();
            return instances[typeof(T)];
        }


        #endregion

        #region definition
        public Type GetMainType()
        {
            return typeof(U);
        }
        public virtual List<Type> DefineManualDependances()
        {
            return new List<Type>();
        }
        public string Name
        {
            get => GetType().Name.Split('`')[0] + "<" + typeof(U).Name.Split('`')[0] + ">";
        }
        public bool IsInit { get; protected set; }

        protected bool printErrorInConsole { get; set; }
        #endregion

        protected PyramidInfo PyramidInfo { get; set; }

        private Dictionary<Type, PyramidInfo> PyramidsInfo = new Dictionary<Type, PyramidInfo>();
        protected Type? RootType { get; set; }
        protected DataManagerConfig? Config { get; set; }

#pragma warning disable CS8618 // Un champ non-nullable doit contenir une valeur non-null lors de la fermeture du constructeur. Envisagez de déclarer le champ comme nullable.
        protected GenericDM()
#pragma warning restore CS8618 // Un champ non-nullable doit contenir une valeur non-null lors de la fermeture du constructeur. Envisagez de déclarer le champ comme nullable.
        {
        }

        #region Config
        public virtual Task<VoidWithError> SetConfiguration(PyramidInfo pyramid, DataManagerConfig config)
        {
            VoidWithError result = new VoidWithError();
            PyramidInfo = pyramid;
            PyramidsInfo[pyramid.type] = pyramid;
            if (pyramid.aliasType != null)
            {
                PyramidsInfo[pyramid.aliasType] = pyramid;
            }
            this.Config = config;
            bool? printError = MustPrintErrorInConsole();
            if (printError != null)
            {
                printErrorInConsole = (bool)printError;
            }
            else
            {
                printErrorInConsole = config.log.printErrorInConsole;
            }
            GetMigrationProvider();
            result = SetDMForType(pyramid, true).ToGeneric();
            return Task.FromResult(result);
        }

        private VoidWithDataError SetDMForType(PyramidInfo pyramid, bool isRoot)
        {
            VoidWithDataError result = new();
            if ((!pyramid.isForceInherit && !pyramid.nonGenericExtension) || !isRoot)
            {
                isRoot = false;
                if (RootType == null)
                {
                    RootType = pyramid.type;
                }
                VoidWithDataError resultTemp = GenericDM.Set(pyramid.type, this);
                if (!resultTemp.Success)
                {
                    return resultTemp;
                }
                PyramidsInfo[pyramid.type] = pyramid;
                if (pyramid.aliasType != null)
                {
                    resultTemp = GenericDM.Set(pyramid.aliasType, this);
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                    PyramidsInfo[pyramid.aliasType] = pyramid;
                }
            }
            foreach (PyramidInfo child in pyramid.children)
            {
                VoidWithDataError resultTemp = SetDMForType(child, isRoot);
                if (!resultTemp.Success)
                {
                    return resultTemp;
                }
            }
            return result;
        }

        public async Task<VoidWithError> Init()
        {
            VoidWithError result = new();
            try
            {
                result = await Initialize();

                if (result.Success)
                {
                    IsInit = true;
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
            return result;
        }
        protected abstract Task<VoidWithError> Initialize();

        internal abstract IMigrationProvider GetMigrationProvider();
        IMigrationProvider IGenericDM.GetMigrationProvider()
        {
            return GetMigrationProvider();
        }

        protected bool? MustPrintErrorInConsole()
        {
            return null;
        }


        public ResultWithError<PyramidInfo> GetPyramidsInfo<X>() where X : U
        {
            ResultWithError<PyramidInfo> result = new();
            if (PyramidsInfo.ContainsKey(typeof(X)))
            {
                result.Result = PyramidsInfo[typeof(X)];
            }
            else
            {
                result.Errors.Add(new DataError(DataErrorCode.PyramidNotFound, "Can't found the pyramid for " + TypeTools.GetReadableName(typeof(X))));
            }
            return result;
        }
        private MethodInfo? IGetPyramidsInfo = null;

        ResultWithError<PyramidInfo> IGenericDM.GetPyramidsInfo<X>()
        {
            try
            {
                ResultWithError<PyramidInfo>? result = InvokeMethod<ResultWithError<PyramidInfo>, X>(ref IGetPyramidsInfo, Array.Empty<object>());
                if (result == null)
                {
                    return new();
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for GetPyramidsInfo<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return new();
            }
        }

        public ResultWithError<DataMemberInfo> GetMemberInfo<X>(string name) where X : U
        {
            ResultWithError<DataMemberInfo> result = new();
            var pyramidQuery = GetPyramidsInfo<X>();
            if (!pyramidQuery.Success || pyramidQuery.Result == null)
            {
                result.Errors = pyramidQuery.Errors;
            }
            else
            {
                PyramidInfo? pyramid = pyramidQuery.Result;
                DataMemberInfo? memberInfo = null;
                while (pyramid != null)
                {
                    memberInfo = pyramid.memberInfo.Find(p => p.Name == name);
                    if (memberInfo != null) break;
                    pyramid = pyramid.parent;
                }

                if (memberInfo == null)
                {
                    result.Errors.Add(new DataError(DataErrorCode.MemberNotFound, "Can't find the member " + name + " on " + TypeTools.GetReadableName(typeof(X))));
                }
                else
                {
                    result.Result = memberInfo;
                }
            }
            return result;
        }
        private MethodInfo? IGetMemberInfo = null;

        ResultWithError<DataMemberInfo> IGenericDM.GetMemberInfo<X>(string name)
        {
            try
            {
                ResultWithError<DataMemberInfo>? result = InvokeMethod<ResultWithError<DataMemberInfo>, X>(ref IGetMemberInfo, new object[] { name });
                if (result == null)
                {
                    return new();
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for GetMemberInfo<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return new();
            }
        }


        public ResultWithError<List<DataMemberInfo>> GetMembersInfo<X, Y>() where X : U
        {
            return GetMembersInfo<X>(typeof(Y));
        }
        public ResultWithError<List<DataMemberInfo>> GetMembersInfo<X>(Type y) where X : U
        {
            ResultWithError<List<DataMemberInfo>> result = new();
            var pyramidQuery = GetPyramidsInfo<X>();
            if (!pyramidQuery.Success || pyramidQuery.Result == null)
            {
                result.Errors = pyramidQuery.Errors;
            }
            else
            {
                PyramidInfo? pyramid = pyramidQuery.Result;
                Dictionary<string, DataMemberInfo> membersInfo = new();
                while (pyramid != null)
                {
                    List<DataMemberInfo> membersTemp = pyramid.memberInfo.FindAll(p => p.Type != null && p.Type.IsAssignableFrom(y));
                    foreach (var memberTemp in membersTemp)
                    {
                        if (!membersInfo.ContainsKey(memberTemp.Name))
                        {
                            membersInfo[memberTemp.Name] = memberTemp;
                        }
                    }
                    pyramid = pyramid.parent;
                }

                result.Result = membersInfo.Values.ToList();
            }
            return result;
        }
        private MethodInfo? IGetMembersInfo = null;
        ResultWithError<List<DataMemberInfo>> IGenericDM.GetMembersInfo<X, Y>()
        {
            try
            {
                Type t = typeof(Y);
                ResultWithError<List<DataMemberInfo>>? result = InvokeMethod<ResultWithError<List<DataMemberInfo>>, X>(ref IGetMembersInfo, new object[] { t });
                if (result == null)
                {
                    return new();
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for GetMembersInfo<" + TypeTools.GetReadableName(typeof(X)) + ", " + TypeTools.GetReadableName(typeof(Y)) + ">");
                return new();
            }
        }

        #endregion

        #region generic query
        public abstract IQueryBuilder<X> CreateQuery<X>() where X : U;

        private MethodInfo? ICreateQuery = null;
        IQueryBuilder<X> IGenericDM.CreateQuery<X>()
        {
            IQueryBuilder<X>? result = InvokeMethod<IQueryBuilder<X>, X>(ref ICreateQuery, Array.Empty<object>());
            if (result == null)
            {
                throw new Exception("Impossible");
            }
            return result;
        }


        #endregion

        #region generic exist
        public abstract IExistBuilder<X> CreateExist<X>() where X : U;
        private MethodInfo? ICreateExist = null;
        IExistBuilder<X> IGenericDM.CreateExist<X>()
        {
            IExistBuilder<X>? result = InvokeMethod<IExistBuilder<X>, X>(ref ICreateExist, Array.Empty<object>());
            if (result == null)
            {
                throw new Exception("Create exist not exist => impossible");
            }
            return result;
        }
        #endregion

        #region generic create
        public abstract ICreateBuilder<X> CreateCreate<X>() where X : U;
        private MethodInfo? ICreateCreate = null;
        ICreateBuilder<X> IGenericDM.CreateCreate<X>()
        {
            ICreateBuilder<X>? result = InvokeMethod<ICreateBuilder<X>, X>(ref ICreateCreate, Array.Empty<object>());
            if (result == null)
            {
                throw new Exception("Create create not exist => impossible");
            }
            return result;
        }
        #endregion

        #region generic update
        public abstract IUpdateBuilder<X> CreateUpdate<X>() where X : U;
        private MethodInfo? ICreateUpdate = null;
        IUpdateBuilder<X> IGenericDM.CreateUpdate<X>()
        {
            IUpdateBuilder<X>? result = InvokeMethod<IUpdateBuilder<X>, X>(ref ICreateUpdate, Array.Empty<object>());
            if (result == null)
            {
                throw new Exception("Create update not exist => impossible");
            }
            return result;
        }
        #endregion

        #region generic delete
        public abstract IDeleteBuilder<X> CreateDelete<X>() where X : U;
        private MethodInfo? ICreateDelete = null;

        IDeleteBuilder<X> IGenericDM.CreateDelete<X>()
        {
            IDeleteBuilder<X>? result = InvokeMethod<IDeleteBuilder<X>, X>(ref ICreateDelete, Array.Empty<object>());
            if (result == null)
            {
                throw new Exception("Create delete not exist => impossible");
            }
            return result;
        }
        #endregion

        #region Get

        #region GetAll
        protected abstract Task<ResultWithError<List<X>>> GetAllLogic<X>() where X : U;

        protected virtual Task<List<GenericError>> CanGetAll()
        {
            return Task.FromResult(new List<GenericError>());
        }
        private async Task WrapperBeforeGetAll(List<GenericError> errors)
        {
            try
            {
                errors.AddRange(await BeforeGetAllWithError());
                await BeforeGetAll();
            }
            catch (Exception e)
            {
                errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> BeforeGetAllWithError()
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task BeforeGetAll()
        {
            return Task.CompletedTask;
        }
        private async Task WrapperAfterGetAll<X>(ResultWithError<List<X>> result) where X : U
        {
            try
            {
                result.Errors.AddRange(await AfterGetAllWithError<X>(result));
                await AfterGetAll<X>(result);
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> AfterGetAllWithError<X>(ResultWithError<List<X>> result) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task AfterGetAll<X>(ResultWithError<List<X>> result) where X : U
        {
            return Task.CompletedTask;
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<ResultWithError<List<U>>> GetAllWithError()
        {
            return GetAllWithError<U>();
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<List<X>>> GetAllWithError<X>() where X : U
        {
            ResultWithError<List<X>> result = new ResultWithError<List<X>>();
            List<GenericError> errors = await CanGetAll();
            if (errors.Count > 0)
            {
                result.Errors = errors;
                PrintErrors(result);
                return result;
            }
            await WrapperBeforeGetAll(errors);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                PrintErrors(result);
                return result;
            }
            result = await GetAllLogic<X>();
            await WrapperAfterGetAll(result);
            PrintErrors(result);
            return result;
        }
        private MethodInfo? IGetAllWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<List<X>>> IGenericDM.GetAllWithError<X>()
        {
            try
            {
                ResultWithError<List<X>>? result = await InvokeMethodAsync<ResultWithError<List<X>>, X>(ref IGetAllWithError, Array.Empty<object>());
                if (result == null)
                {
                    result = new ResultWithError<List<X>>();
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method GetAllWithError"));
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<List<X>> result = new ResultWithError<List<X>>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<List<U>> GetAll()
        {
            return GetAll<U>();
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<List<X>> GetAll<X>() where X : U
        {
            ResultWithError<List<X>> result = await GetAllWithError<X>();
            if (result.Success && result.Result != null)
            {
                return result.Result;
            }
            return new List<X>();
        }
        private MethodInfo? IGetAll = null;

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<List<X>> IGenericDM.GetAll<X>()
        {
            try
            {
                List<X>? result = await InvokeMethodAsync<List<X>, X>(ref IGetAll, Array.Empty<object>());
                if (result == null)
                {
                    return new List<X>();
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for GetAll<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return new List<X>();
            }
        }

        #endregion

        #region GetById
        protected abstract Task<ResultWithError<X>> GetByIdLogic<X>(int id) where X : U;

        protected virtual Task<List<GenericError>> CanGetById(int id)
        {
            return Task.FromResult(new List<GenericError>());
        }
        private async Task WrapperBeforeGetById(int id, List<GenericError> errors)
        {
            try
            {
                errors.AddRange(await BeforeGetByIdWithError(id));
                await BeforeGetById(id);
            }
            catch (Exception e)
            {
                errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> BeforeGetByIdWithError(int id)
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task BeforeGetById(int id)
        {
            return Task.CompletedTask;
        }
        private async Task WrapperAfterGetById<X>(int id, ResultWithError<X> result) where X : U
        {
            try
            {
                result.Errors.AddRange(await AfterGetByIdWithError(id, result));
                await AfterGetById(id, result);
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> AfterGetByIdWithError<X>(int id, ResultWithError<X> result) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task AfterGetById<X>(int id, ResultWithError<X> result) where X : U
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<ResultWithError<U>> GetByIdWithError(int id)
        {
            return GetByIdWithError<U>(id);
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<X>> GetByIdWithError<X>(int id) where X : U
        {
            ResultWithError<X> result = new ResultWithError<X>();
            try
            {
                List<GenericError> errors = await CanGetById(id);
                if (errors.Count > 0)
                {
                    result.Errors = errors;
                    PrintErrors(result);
                    return result;
                }
                await WrapperBeforeGetById(id, errors);
                if (errors.Count > 0)
                {
                    result.Errors = errors;
                    PrintErrors(result);
                    return result;
                }
                result = await GetByIdLogic<X>(id);
                await WrapperAfterGetById(id, result);
            }
            catch (Exception ex)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, ex));
            }
            PrintErrors(result);
            return result;
        }
        private MethodInfo? IGetByIdWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<X>> IGenericDM.GetByIdWithError<X>(int id)
        {
            try
            {
                ResultWithError<X>? result = await InvokeMethodAsync<ResultWithError<X>, X>(ref IGetByIdWithError, new object[] { id });
                if (result == null)
                {
                    result = new ResultWithError<X>();
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method GetByIdWithError"));
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<X> result = new ResultWithError<X>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<U?> GetById(int id)
        {
            return GetById<U>(id);
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<X?> GetById<X>(int id) where X : U
        {
            ResultWithError<X> result = await GetByIdWithError<X>(id);
            if (result.Success)
            {
                return result.Result;
            }
            return default;
        }
        private MethodInfo? IGetById = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
#pragma warning disable CS8616 // Nullability of reference types in return type doesn't match implemented member.
        async Task<X> IGenericDM.GetById<X>(int id)
#pragma warning restore CS8616 // Nullability of reference types in return type doesn't match implemented member.
        {
            try
            {
                X? result = await InvokeMethodAsync<X, X>(ref IGetById, new object[] { id });
                if (result == null)
                {
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                    return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for GetById<" + TypeTools.GetReadableName(typeof(X)) + ">");
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
            }
        }

        async Task<object?> IGenericDM.GetById(int id)
        {
            return await GetById<U>(id);
        }
        #endregion

        #region GetByIds
        protected abstract Task<ResultWithError<List<X>>> GetByIdsLogic<X>(List<int> ids) where X : U;

        protected virtual Task<List<GenericError>> CanGetByIds(List<int> ids)
        {
            return Task.FromResult(new List<GenericError>());
        }
        private async Task WrapperBeforeGetByIds(List<int> ids, List<GenericError> errors)
        {
            try
            {
                errors.AddRange(await BeforeGetByIdsWithError(ids));
                await BeforeGetByIds(ids);
            }
            catch (Exception e)
            {
                errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> BeforeGetByIdsWithError(List<int> ids)
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task BeforeGetByIds(List<int> ids)
        {
            return Task.CompletedTask;
        }
        private async Task WrapperAfterGetByIds<X>(List<int> ids, ResultWithError<List<X>> result) where X : U
        {
            try
            {
                result.Errors.AddRange(await AfterGetByIdsWithError(ids, result));
                await AfterGetByIds(ids, result);
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> AfterGetByIdsWithError<X>(List<int> ids, ResultWithError<List<X>> result) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task AfterGetByIds<X>(List<int> ids, ResultWithError<List<X>> result) where X : U
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<ResultWithError<List<U>>> GetByIdsWithError(List<int> ids)
        {
            return GetByIdsWithError<U>(ids);
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<List<X>>> GetByIdsWithError<X>(List<int> ids) where X : U
        {
            ResultWithError<List<X>> result = new ResultWithError<List<X>>();
            if (ids.Count == 0)
            {
                result.Result = new List<X>();
                return result;
            }
            List<GenericError> errors = await CanGetByIds(ids);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                PrintErrors(result);
                return result;
            }
            await WrapperBeforeGetByIds(ids, errors);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                PrintErrors(result);
                return result;
            }
            result = await GetByIdsLogic<X>(ids);
            await WrapperAfterGetByIds(ids, result);
            PrintErrors(result);
            return result;
        }
        private MethodInfo? IGetByIdsWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<List<X>>> IGenericDM.GetByIdsWithError<X>(List<int> ids)
        {
            try
            {
                ResultWithError<List<X>>? result = await InvokeMethodAsync<ResultWithError<List<X>>, X>(ref IGetByIdsWithError, new object[] { ids });
                if (result == null)
                {
                    result = new ResultWithError<List<X>>();
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method GetByIdsWithError"));
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<List<X>> result = new ResultWithError<List<X>>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<List<U>?> GetByIds(List<int> ids)
        {
            return GetByIds<U>(ids);
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<List<X>?> GetByIds<X>(List<int> ids) where X : U
        {
            ResultWithError<List<X>> result = await GetByIdsWithError<X>(ids);
            if (result.Success)
            {
                return result.Result;
            }
            return default;
        }
        private MethodInfo? IGetByIds = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<List<X>> IGenericDM.GetByIds<X>(List<int> ids)
        {
            try
            {
                List<X>? result = await InvokeMethodAsync<List<X>, X>(ref IGetByIds, new object[] { ids });
                if (result == null)
                {
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                    return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for GetByIds<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return new List<X>();
            }
        }
        #endregion

        #region Where
        protected abstract Task<ResultWithError<List<X>>> WhereLogic<X>(Expression<Func<X, bool>> func) where X : U;

        protected virtual Task<List<GenericError>> CanWhere<X>(Expression<Func<X, bool>> func) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        private async Task WrapperBeforeWhere<X>(Expression<Func<X, bool>> func, List<GenericError> errors) where X : U
        {
            try
            {
                errors.AddRange(await BeforeWhereWithError(func));
                await BeforeWhere(func);
            }
            catch (Exception e)
            {
                errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> BeforeWhereWithError<X>(Expression<Func<X, bool>> func) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task BeforeWhere<X>(Expression<Func<X, bool>> func) where X : U
        {
            return Task.CompletedTask;
        }
        private async Task WrapperAfterWhere<X>(Expression<Func<X, bool>> func, ResultWithError<List<X>> result) where X : U
        {
            try
            {
                result.Errors.AddRange(await AfterWhereWithError(func, result));
                await AfterWhere(func, result);
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> AfterWhereWithError<X>(Expression<Func<X, bool>> func, ResultWithError<List<X>> result) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task AfterWhere<X>(Expression<Func<X, bool>> func, ResultWithError<List<X>> result) where X : U
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<ResultWithError<List<U>>> WhereWithError(Expression<Func<U, bool>> func)
        {
            return WhereWithError<U>(func);
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<List<X>>> WhereWithError<X>(Expression<Func<X, bool>> func) where X : U
        {
            ResultWithError<List<X>> result = new ResultWithError<List<X>>();
            List<GenericError> errors = await CanWhere(func);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                PrintErrors(result);
                return result;
            }
            await WrapperBeforeWhere(func, errors);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                PrintErrors(result);
                return result;
            }
            result = await WhereLogic(func);
            await WrapperAfterWhere(func, result);
            PrintErrors(result);
            return result;
        }
        private MethodInfo? IWhereWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<List<X>>> IGenericDM.WhereWithError<X>(Expression<Func<X, bool>> func)
        {
            try
            {
                ResultWithError<List<X>>? result = await InvokeMethodAsync<ResultWithError<List<X>>, X>(ref IWhereWithError, new object[] { func });
                if (result == null)
                {
                    result = new ResultWithError<List<X>>();
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method WhereWithError"));
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<List<X>> result = new ResultWithError<List<X>>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<List<U>> Where(Expression<Func<U, bool>> func)
        {
            return Where<U>(func);
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<List<X>> Where<X>(Expression<Func<X, bool>> func) where X : U
        {
            ResultWithError<List<X>> result = await WhereWithError(func);
            if (result.Success && result.Result != null)
            {
                return result.Result;
            }
            return new List<X>();
        }
        private MethodInfo? IWhere = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<List<X>> IGenericDM.Where<X>(Expression<Func<X, bool>> func)
        {
            try
            {
                List<X>? result = await InvokeMethodAsync<List<X>, X>(ref IWhere, new object[] { func }, false);
                if (result == null)
                {
                    return new List<X>();
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for Where<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return new List<X>();
            }
        }
        #endregion

        #region single
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<ResultWithError<U>> SingleWithError(Expression<Func<U, bool>> func)
        {
            return SingleWithError<U>(func);
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<X>> SingleWithError<X>(Expression<Func<X, bool>> func) where X : U
        {
            ResultWithError<X> result = new ResultWithError<X>();
            ResultWithError<List<X>> where = await WhereWithError<X>(func);

            result.Errors = where.Errors;
            if (where.Result != null && where.Result.Count > 0)
            {
                result.Result = where.Result[0];
            }
            return result;
        }
        private MethodInfo? ISingleWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<X>> IGenericDM.SingleWithError<X>(Expression<Func<X, bool>> func)
        {
            try
            {
                ResultWithError<X>? result = await InvokeMethodAsync<ResultWithError<X>, X>(ref ISingleWithError, new object[] { func });
                if (result == null)
                {
                    result = new ResultWithError<X>();
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method SingleWithError"));
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<X> result = new ResultWithError<X>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<U?> Single(Expression<Func<U, bool>> func)
        {
            return Single<U>(func);
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<X?> Single<X>(Expression<Func<X, bool>> func) where X : U
        {
            List<X> where = await Where(func);
            if (where.Count > 0)
            {
                return where[0];
            }
            return default;
        }

        private MethodInfo? ISingle = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
#pragma warning disable CS8616 // Nullability of reference types in return type doesn't match implemented member.
        async Task<X> IGenericDM.Single<X>(Expression<Func<X, bool>> func)
#pragma warning restore CS8616 // Nullability of reference types in return type doesn't match implemented member.
        {
            try
            {
                X? result = await InvokeMethodAsync<X?, X>(ref ISingle, new object[] { func }, false);
                if (result == null)
                {
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                    return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for Single<" + TypeTools.GetReadableName(typeof(X)) + ">");
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
            }
        }

        #endregion

        private MethodInfo? IOnItemLoaded = null;

        /// <summary>
        /// Trigger when a item is converter into real object
        /// </summary>
        /// <typeparam name="X"></typeparam>
        /// <param name="item"></param>
        public virtual Task OnItemLoaded<X>(X item) where X : U
        {
            return Task.CompletedTask;
        }

        async Task IGenericDM.OnItemLoaded<X>(X item)
        {
            try
            {
                await InvokeMethodTask<X>(ref IOnItemLoaded, new object[] { item });
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for OnItemLoaded<" + TypeTools.GetReadableName(typeof(X)) + ">");
            }
        }



        #endregion

        #region Exist
        protected virtual Task<ResultWithError<bool>> ExistLogic<X>(
            Expression<Func<X, bool>> func) where X : U
        {
            return CreateExist<X>().Where(func).RunWithError();
        }

        public Task<ResultWithError<bool>> ExistWithError(Expression<Func<U, bool>> func)
        {
            return ExistLogic(func);
        }
        public Task<ResultWithError<bool>> ExistWithError<X>(Expression<Func<X, bool>> func) where X : U
        {
            return ExistLogic(func);
        }
        private MethodInfo? IExistWithError = null;
        async Task<ResultWithError<bool>> IGenericDM.ExistWithError<X>(Expression<Func<X, bool>> func)
        {
            try
            {
                ResultWithError<bool>? result = await InvokeMethodAsync<ResultWithError<bool>, X>(ref IExistWithError, [func], false);
                if (result == null)
                {
                    result = new ResultWithError<bool>();
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method ExistWithError"));
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<bool> result = new ResultWithError<bool>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        public async Task<bool> Exist(Expression<Func<U, bool>> func)
        {
            ResultWithError<bool> result = await ExistWithError(func);
            return result.Success && result.Result;
        }
        public async Task<bool> Exist<X>(Expression<Func<X, bool>> func) where X : U
        {
            ResultWithError<bool> result = await ExistWithError(func);
            return result.Success && result.Result;
        }
        private MethodInfo? IExist = null;
        Task<bool> IGenericDM.Exist<X>(Expression<Func<X, bool>> func)
        {
            return InvokeMethodAsync<bool, X>(ref IExist, [func], false);
        }
        #endregion

        #region Create

        public event OnCreatedHandler<U> OnCreated;

        #region List
        protected abstract Task<VoidWithError> BulkCreateLogic<X>(List<X> values, bool withId) where X : U;
        protected abstract Task<ResultWithError<List<X>>> CreateLogic<X>(List<X> values) where X : U;
        protected virtual Task<List<GenericError>> CanCreate<X>(List<X> values) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        private async Task WrapperBeforeCreate<X>(List<X> values, List<GenericError> errors) where X : U
        {
            try
            {
                errors.AddRange(await BeforeCreateWithError(values));
                await BeforeCreate(values);
            }
            catch (Exception e)
            {
                errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> BeforeCreateWithError<X>(List<X> values) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task BeforeCreate<X>(List<X> values) where X : U
        {
            return Task.CompletedTask;
        }
        private async Task WrapperAfterCreate<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            try
            {
                result.Errors.AddRange(await AfterCreateWithError(values, result));
                await AfterCreate(values, result);
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }

        protected virtual Task<List<GenericError>> AfterCreateWithError<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task AfterCreate<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<List<X>>> CreateWithError<X>(List<X> values) where X : U
        {
            ResultWithError<List<X>> result = new ResultWithError<List<X>>();
            List<GenericError> errors = await CanCreate(values);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                return result;
            }
            await WrapperBeforeCreate(values, errors);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                return result;
            }
            result = await CreateLogic(values);
            await WrapperAfterCreate(values, result);
            PublishCreated(TransformResult<X, U>(result));
            return result;
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<List<X>>> IGenericDM.CreateWithError<X>(List<X> values)
        {
            try
            {
                ResultWithError<List<X>> result = new();

                List<U> valuesTemp = TransformList<X, U>(values);
                ResultWithError<List<U>>? resultTemp = await CreateWithError(valuesTemp);
                if (resultTemp != null)
                {
                    if (resultTemp.Result is List<U> castedList)
                    {
                        result.Result = TransformList<U, X>(castedList);
                    }
                    result.Errors = resultTemp.Errors;
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method CreateWithError"));
                }

                return result;
            }
            catch (Exception e)
            {
                ResultWithError<List<X>> result = new ResultWithError<List<X>>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public Task<VoidWithError> BulkCreateWithError<X>(List<X> values, bool withId = false) where X : U
        {
            return BulkCreateLogic(values, withId);
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<VoidWithError> IGenericDM.BulkCreateWithError<X>(List<X> values, bool withId)
        {
            try
            {
                List<U> valuesTemp = TransformList<X, U>(values);
                return await BulkCreateWithError(valuesTemp, withId);
            }
            catch (Exception e)
            {
                VoidWithError result = new VoidWithError();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<List<X>> Create<X>(List<X> values) where X : U
        {
            ResultWithError<List<X>> result = await CreateWithError(values);
            if (result.Success && result.Result != null)
            {
                return result.Result;
            }
            return new List<X>();
        }
        private MethodInfo? ICreateList = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<List<X>> IGenericDM.Create<X>(List<X> values)
        {
            try
            {
                List<X> result = new();
                List<U> valuesTemp = TransformList<X, U>(values);
                List<U>? resultTemp = await InvokeMethodAsync<List<U>, U>(ref ICreateList, new object[] { valuesTemp });
                if (resultTemp != null)
                {
                    return TransformList<U, X>(resultTemp);
                }
                return result;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for Create<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return new List<X>();
            }
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<bool> BulkCreate<X>(List<X> values, bool withId = false) where X : U
        {
            return (await BulkCreateWithError(values, withId)).Success;
        }
        private MethodInfo? IBulkCreateList = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<bool> IGenericDM.BulkCreate<X>(List<X> values, bool withId)
        {
            try
            {
                List<U> valuesTemp = TransformList<X, U>(values);
                bool? resultTemp = await InvokeMethodAsync<bool, U>(ref IBulkCreateList, new object[] { valuesTemp, withId });
                return resultTemp ?? false;
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for BulkCreate<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return false;
            }
        }

        #endregion

        #region Item
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<X>> CreateWithError<X>(X value) where X : U
        {
            ResultWithError<X> result = new();
            ResultWithError<List<X>> resultList = await CreateWithError(new List<X>() { value });
            result.Errors = resultList.Errors;
            if (resultList.Result?.Count > 0)
            {
                result.Result = resultList.Result[0];
            }
            else
            {
                result.Result = default;
            }
            return result;
        }
        private MethodInfo? ICreateWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<X>> IGenericDM.CreateWithError<X>(X value)
        {
            try
            {
                ResultWithError<X> result = new();
                if (value is U)
                {
                    ResultWithError<U>? resultTemp = await InvokeMethodAsync<ResultWithError<U>, U>(ref ICreateWithError, new object[] { value });
                    if (resultTemp != null)
                    {
                        if (resultTemp.Result is X casted)
                        {
                            result.Result = casted;
                        }
                        result.Errors = resultTemp.Errors;
                    }
                    else
                    {
                        result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method CreateWithError"));
                    }
                    return result;
                }
                result.Errors.Add(new DataError(DataErrorCode.NoItemProvided, "You must provide a value to create"));
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<X> result = new ResultWithError<X>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<X?> Create<X>(X value) where X : U
        {
            ResultWithError<X> result = await CreateWithError(value);
            if (result.Success)
            {
                return result.Result;
            }
            return default;
        }
        private MethodInfo? ICreate = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
#pragma warning disable CS8616 // Nullability of reference types in return type doesn't match implemented member.
        async Task<X> IGenericDM.Create<X>(X value)
#pragma warning restore CS8616 // Nullability of reference types in return type doesn't match implemented member.
        {
            try
            {
                if (value is U)
                {
                    U? result = await InvokeMethodAsync<U, U>(ref ICreate, new object[] { value });
                    if (result is X resultCasted)
                    {
                        return resultCasted;
                    }
                }
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for Create<" + TypeTools.GetReadableName(typeof(X)) + ">");
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
            }
        }
        #endregion

        #endregion

        #region Update

        public event OnUpdatedHandler<U> OnUpdated;

        #region List
        protected abstract Task<ResultWithError<List<X>>> UpdateLogic<X>(List<X> values) where X : U;
        protected virtual Task<List<GenericError>> CanUpdate<X>(List<X> values) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        private async Task WrapperBeforeUpdate<X>(List<X> values, List<GenericError> errors) where X : U
        {
            try
            {
                errors.AddRange(await BeforeUpdateWithError(values));
                await BeforeUpdate(values);
            }
            catch (Exception e)
            {
                errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> BeforeUpdateWithError<X>(List<X> values) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task BeforeUpdate<X>(List<X> values) where X : U
        {
            return Task.CompletedTask;
        }
        private async Task WrapperAfterUpdate<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            try
            {
                result.Errors.AddRange(await AfterUpdateWithError(values, result));
                await AfterUpdate(values, result);
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> AfterUpdateWithError<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task AfterUpdate<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<List<X>>> UpdateWithError<X>(List<X> values) where X : U
        {
            ResultWithError<List<X>> result = new ResultWithError<List<X>>();
            List<GenericError> errors = await CanUpdate(values);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                return result;
            }
            await WrapperBeforeUpdate(values, errors);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                return result;
            }
            result = await UpdateLogic(values);
            await WrapperAfterUpdate(values, result);
            PublishUpdated(TransformResult<X, U>(result));
            return result;
        }
        private MethodInfo? IUpdateListWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<List<X>>> IGenericDM.UpdateWithError<X>(List<X> values)
        {
            try
            {
                ResultWithError<List<X>> result = new();
                List<U> valuesTemp = TransformList<X, U>(values);
                ResultWithError<List<U>>? resultTemp = await InvokeMethodAsync<ResultWithError<List<U>>, U>(ref IUpdateListWithError, new object[] { valuesTemp });
                if (resultTemp != null)
                {
                    if (resultTemp.Result is List<U> castedList)
                    {
                        result.Result = TransformList<U, X>(castedList);
                    }
                    result.Errors = resultTemp.Errors;
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method UpdateWithError"));
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<List<X>> result = new ResultWithError<List<X>>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<List<X>> Update<X>(List<X> values) where X : U
        {
            ResultWithError<List<X>> result = await UpdateWithError(values);
            if (result.Success && result.Result != null)
            {
                return result.Result;
            }
            return new List<X>();
        }
        private MethodInfo? IUpdateList = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<List<X>> IGenericDM.Update<X>(List<X> values)
        {
            try
            {
                List<U> valuesTemp = TransformList<X, U>(values);
                List<U>? result = await InvokeMethodAsync<List<U>, U>(ref IUpdateList, new object[] { valuesTemp });
                if (result != null)
                {
                    return TransformList<U, X>(result);
                }
                return new List<X>();
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for Update<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return new List<X>();
            }
        }

        // todo maybe add a function to update without reload to optimize request (be aware for all DM)
        // public VoidWithError UpdateWithErrorNoReload<X>(List<X> values)
        #endregion

        #region Item
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<X>> UpdateWithError<X>(X value) where X : U
        {
            ResultWithError<X> result = new();
            ResultWithError<List<X>> resultList = await UpdateWithError(new List<X>() { value });
            result.Errors = resultList.Errors;
            if (resultList.Result?.Count > 0)
            {
                result.Result = resultList.Result[0];
            }
            else
            {
                result.Result = default;
            }
            return result;
        }
        private MethodInfo? IUpdateWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<X>> IGenericDM.UpdateWithError<X>(X value)
        {
            try
            {
                ResultWithError<X> result = new();
                if (value is U)
                {
                    ResultWithError<U>? resultTemp = await InvokeMethodAsync<ResultWithError<U>, U>(ref IUpdateWithError, new object[] { value });
                    if (resultTemp != null)
                    {
                        if (resultTemp.Result is X castedItem)
                        {
                            result.Result = castedItem;
                        }
                        result.Errors = resultTemp.Errors;
                    }
                    else
                    {
                        result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method UpdateWithError"));
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<X> result = new ResultWithError<X>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<X?> Update<X>(X value) where X : U
        {
            ResultWithError<X> result = await UpdateWithError(value);
            if (result.Success)
            {
                return result.Result;
            }
            return default;
        }
        private MethodInfo? IUpdate = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<X> IGenericDM.Update<X>(X value)
        {
            try
            {
                if (value is U)
                {
                    U? result = await InvokeMethodAsync<U, U>(ref IUpdate, new object[] { value });
                    if (result is X casted)
                    {
                        return casted;
                    }
                }
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for Update<" + TypeTools.GetReadableName(typeof(X)) + ">");
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
            }
        }
        #endregion

        #endregion

        #region Delete

        public event OnDeletedHandler<U> OnDeleted;

        #region List
        protected abstract Task<ResultWithError<List<X>>> DeleteLogic<X>(List<X> values) where X : U;
        protected virtual Task<List<GenericError>> CanDelete<X>(List<X> values) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        private async Task WrapperBeforeDelete<X>(List<X> values, List<GenericError> errors) where X : U
        {
            try
            {
                errors.AddRange(await BeforeDeleteWithError(values));
                await BeforeDelete(values);
            }
            catch (Exception e)
            {
                errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> BeforeDeleteWithError<X>(List<X> values) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task BeforeDelete<X>(List<X> values) where X : U
        {
            return Task.CompletedTask;
        }
        private async Task WrapperAfterDelete<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            try
            {
                result.Errors.AddRange(await AfterDeleteWithError(values, result));
                await AfterDelete(values, result);
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }
        }
        protected virtual Task<List<GenericError>> AfterDeleteWithError<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            return Task.FromResult(new List<GenericError>());
        }
        protected virtual Task AfterDelete<X>(List<X> values, ResultWithError<List<X>> result) where X : U
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<List<X>>> DeleteWithError<X>(List<X> values) where X : U
        {
            ResultWithError<List<X>> result = new ResultWithError<List<X>>();
            if (values.Count == 0)
            {
                result.Result = values;
                return result;
            }
            List<GenericError> errors = await CanDelete(values);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                return result;
            }
            await WrapperBeforeDelete(values, errors);
            if (errors.Count > 0)
            {
                result.Errors = errors;
                return result;
            }
            result = await DeleteLogic(values);
            await WrapperAfterDelete(values, result);
            PublishDeleted(TransformResult<X, U>(result));
            return result;
        }

        private void PublishCreated(ResultWithError<List<U>> result)
        {
            if (OnCreated == null) return;
            foreach (OnCreatedHandler<U> handler in OnCreated.GetInvocationList())
            {
                try
                {
                    handler(result);
                }
                catch (Exception exception)
                {
                    AventusLogger.Instance.LogError(
                        exception,
                        "An OnCreated handler crashed for " + TypeTools.GetReadableName(typeof(U)));
                }
            }
        }

        private void PublishUpdated(ResultWithError<List<U>> result)
        {
            if (OnUpdated == null) return;
            foreach (OnUpdatedHandler<U> handler in OnUpdated.GetInvocationList())
            {
                try
                {
                    handler(result);
                }
                catch (Exception exception)
                {
                    AventusLogger.Instance.LogError(
                        exception,
                        "An OnUpdated handler crashed for " + TypeTools.GetReadableName(typeof(U)));
                }
            }
        }

        private void PublishDeleted(ResultWithError<List<U>> result)
        {
            if (OnDeleted == null) return;
            foreach (OnDeletedHandler<U> handler in OnDeleted.GetInvocationList())
            {
                try
                {
                    handler(result);
                }
                catch (Exception exception)
                {
                    AventusLogger.Instance.LogError(
                        exception,
                        "An OnDeleted handler crashed for " + TypeTools.GetReadableName(typeof(U)));
                }
            }
        }
        private MethodInfo? IDeleteListWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<List<X>>> IGenericDM.DeleteWithError<X>(List<X> values)
        {
            try
            {
                ResultWithError<List<X>> result = new();
                List<U> valuesTemp = TransformList<X, U>(values);
                ResultWithError<List<U>>? resultTemp = await InvokeMethodAsync<ResultWithError<List<U>>, U>(ref IDeleteListWithError, new object[] { valuesTemp });
                if (resultTemp != null)
                {
                    if (resultTemp.Result is List<U> castedList)
                    {
                        result.Result = TransformList<U, X>(castedList);
                    }
                    result.Errors = resultTemp.Errors;
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method DeleteWithError"));
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<List<X>> result = new ResultWithError<List<X>>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<List<X>> Delete<X>(List<X> values) where X : U
        {
            ResultWithError<List<X>> result = await DeleteWithError(values);
            if (result.Success && result.Result != null)
            {
                return result.Result;
            }
            return new List<X>();
        }
        private MethodInfo? IDeleteList = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<List<X>> IGenericDM.Delete<X>(List<X> values)
        {
            try
            {
                List<U> valuesTemp = TransformList<X, U>(values);
                List<U>? result = await InvokeMethodAsync<List<U>, U>(ref IDeleteList, new object[] { valuesTemp });
                if (result != null)
                {
                    return TransformList<U, X>(result);
                }
                return new List<X>();
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for Delete<" + TypeTools.GetReadableName(typeof(X)) + ">");
                return new List<X>();
            }
        }
        #endregion

        #region Item
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<ResultWithError<X>> DeleteWithError<X>(X value) where X : U
        {
            ResultWithError<X> result = new();
            ResultWithError<List<X>> resultList = await DeleteWithError(new List<X>() { value });
            result.Errors = resultList.Errors;
            if (resultList.Result?.Count > 0)
            {
                result.Result = resultList.Result[0];
            }
            else
            {
                result.Result = default;
            }
            return result;
        }
        private MethodInfo? IDeleteWithError = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<ResultWithError<X>> IGenericDM.DeleteWithError<X>(X value)
        {
            try
            {
                ResultWithError<X> result = new();
                if (value is U)
                {
                    ResultWithError<U>? resultTemp = await InvokeMethodAsync<ResultWithError<U>, U>(ref IDeleteWithError, new object[] { value });
                    if (resultTemp != null)
                    {
                        if (resultTemp.Result is X castedItem)
                        {
                            result.Result = castedItem;
                        }
                        result.Errors = resultTemp.Errors;
                    }
                    else
                    {
                        result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Can't found the method DeleteWithError"));
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                ResultWithError<X> result = new ResultWithError<X>();
                if (e is AventusException aventusException)
                {
                    result.Errors.Add(aventusException.Error);
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
                }
                return result;
            }
        }
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public async Task<X?> Delete<X>(X value) where X : U
        {
            ResultWithError<X> result = await DeleteWithError(value);
            if (result.Success)
            {
                return result.Result;
            }
            return default;
        }
        private MethodInfo? IDelete = null;
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        async Task<X> IGenericDM.Delete<X>(X value)
        {
            try
            {
                if (value is U)
                {
                    U? result = await InvokeMethodAsync<U, U>(ref IDelete, new object[] { value });
                    if (result is X casted)
                    {
                        return casted;
                    }
                }
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
            }
            catch (Exception e)
            {
                AventusLogger.Instance.LogError(exception: e, message: "InvokeMethod crashed for Delete<" + TypeTools.GetReadableName(typeof(X)) + ">");
#pragma warning disable CS8603 // Existence possible d'un retour de référence null.
                return default;
#pragma warning restore CS8603 // Existence possible d'un retour de référence null.
            }
        }
        #endregion

        #endregion

        #region Transaction
        protected abstract TransactionContext? getTransactionScope();
        protected abstract void setTransactionScope(TransactionContext? context);
        /// <summary>
        /// Run a function inside a transaction that ll be commit if no error otherwise rollback
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="defaultValue"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public async Task<ResultWithError<Y>> RunInsideTransaction<Y>(Y? defaultValue, Func<Task<ResultWithError<Y>>> action)
        {
            ResultWithError<TransactionContext> transactionResult = (await BeginTransaction()).ToGeneric();
            if (!transactionResult.Success || transactionResult.Result == null)
            {
                ResultWithError<Y> resultError = new()
                {
                    Result = defaultValue,
                    Errors = transactionResult.Errors
                };
                return resultError;
            }
            setTransactionScope(transactionResult.Result);
            ResultWithError<Y> resultTemp;
            try
            {
                resultTemp = await action();
            }
            catch (Exception exception)
            {
                resultTemp = new ResultWithError<Y>()
                {
                    Result = defaultValue
                };
                resultTemp.Errors.Add(new DataError(DataErrorCode.UnknowError, exception));
            }
            if (resultTemp.Success)
            {
                ResultWithError<bool> commitResult = await transactionResult.Result.Commit();
                resultTemp.Errors.AddRange(commitResult.Errors);
                if (commitResult.Result || !commitResult.Success)
                {
                    setTransactionScope(null);
                }
            }
            else
            {
                ResultWithError<bool> rollbackResult = await transactionResult.Result.Rollback();
                resultTemp.Errors.AddRange(rollbackResult.Errors);
                setTransactionScope(null);
            }
            return resultTemp;
        }
        /// <summary>
        /// Run a function inside a transaction that ll be commit if no error otherwise rollback
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public Task<ResultWithError<Y>> RunInsideTransaction<Y>(Func<Task<ResultWithError<Y>>> action)
        {
            return RunInsideTransaction<Y>(default, action);
        }
        /// <summary>
        /// Run a function inside a transaction that ll be commit if no error otherwise rollback
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public async Task<VoidWithError> RunInsideTransaction(Func<Task<VoidWithError>> action)
        {
            ResultWithError<TransactionContext> transactionResult = (await BeginTransaction()).ToGeneric();
            if (!transactionResult.Success || transactionResult.Result == null)
            {
                VoidWithError resultError = new()
                {
                    Errors = transactionResult.Errors
                };
                return resultError;
            }
            setTransactionScope(transactionResult.Result);
            VoidWithError resultTemp;
            try
            {
                resultTemp = await action();
            }
            catch (Exception exception)
            {
                resultTemp = new VoidWithError();
                resultTemp.Errors.Add(new DataError(DataErrorCode.UnknowError, exception));
            }
            if (resultTemp.Success)
            {
                ResultWithError<bool> commitResult = await transactionResult.Result.Commit();
                resultTemp.Errors.AddRange(commitResult.Errors);
                if (commitResult.Result || !commitResult.Success)
                {
                    setTransactionScope(null);
                }
            }
            else
            {
                ResultWithError<bool> rollbackResult = await transactionResult.Result.Rollback();
                resultTemp.Errors.AddRange(rollbackResult.Errors);
                setTransactionScope(null);
            }
            return resultTemp;
        }

        protected abstract Task<ResultWithError<TransactionContext>> BeginTransactionScope();
        protected abstract Task EndTransactionScope();
        protected async Task<ResultWithError<TransactionContext>> BeginTransaction()
        {
            ResultWithError<TransactionContext> result = new();
            try
            {

                TransactionContext? transactionContext = getTransactionScope();
                if (transactionContext == null)
                {
                    await result.RunAsync(() => BeginTransactionScope());
                }
                else
                {
                    transactionContext.count++;
                    result.Result = transactionContext;
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new DataError(DataErrorCode.UnknowError, e));
            }


            return result;
        }

        protected async Task EndTransaction()
        {
            if (getTransactionScope() != null)
            {
                await EndTransactionScope();
                setTransactionScope(null);
            }
        }

        #endregion

        #region Utils
        protected List<Y> TransformList<X, Y>(List<X> input)
        {
            return input.ToList<Y>();
        }
        protected ResultWithError<List<Y>> TransformResult<X, Y>(ResultWithError<List<X>> result)
        {
            ResultWithError<List<Y>> transformed = new ResultWithError<List<Y>>();
            transformed.Errors = result.Errors.ToList();
            if (result.Result != null)
            {
                transformed.Result = TransformList<X, Y>(result.Result);
            }
            return transformed;
        }

        protected X? InvokeMethod<X, Y>(ref MethodInfo? methodSaved, object[]? parameters = null, bool checkSameParam = true, [CallerMemberName] string name = "")
        {
            if (methodSaved != null)
            {
                Type YType = typeof(Y);
                MethodInfo methodType = methodSaved.MakeGenericMethod(YType);
                X? result = (X?)methodType.Invoke(this, parameters);
                return result;
            }
            bool mustThrow = false;
            parameters ??= Array.Empty<object>();
            List<Type> types = new();
            foreach (object param in parameters)
            {
                Type type = param.GetType();
                if (param is Expression exp && type.IsGenericType)
                {
                    Type[] t = exp.Type.GetGenericArguments();
                    Type fctType = t.Length switch
                    {
                        1 => typeof(Func<>),
                        2 => typeof(Func<,>),
                        _ => throw new NotImplementedException()
                    };
                    fctType = fctType.MakeGenericType(t);
                    type = typeof(Expression<>).MakeGenericType(fctType);
                }
                types.Add(type);
            }

            MethodInfo[] methods = this.GetType().GetMethods();
            foreach (MethodInfo method in methods)
            {
                if (method.Name == name && method.IsGenericMethod)
                {
                    try
                    {
                        Type YType = typeof(Y);
                        MethodInfo methodType = method.MakeGenericMethod(YType);
                        mustThrow = true;
                        // it ll fail if Generic constraint are different but we can't deal it properly inside code so let the compiler do the job
                        if (checkSameParam)
                        {
                            if (GenericDM<T, U>.IsSameParameters(methodType.GetParameters(), types))
                            {
                                X? result = (X?)methodType.Invoke(this, parameters);
                                return result;
                            }
                        }
                        else
                        {
                            X? result = (X?)methodType.Invoke(this, parameters);
                            return result;
                        }
                    }
                    catch (Exception e)
                    {
                        if (mustThrow)
                        {
                            PrintErrors(e);
#pragma warning disable CA2200 // Rethrow to preserve stack details
                            throw e;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                        }
                    }
                }
            }

            throw new DataError(DataErrorCode.MethodNotFound, "The method " + name + "(" + string.Join(", ", parameters.Select(p => p.GetType().Name)) + ") can't be found or failed").GetException();
        }

        protected Task<X?> InvokeMethodAsync<X, Y>(ref MethodInfo? methodSaved, object[]? parameters = null, bool checkSameParam = true, [CallerMemberName] string name = "")
        {
            if (methodSaved != null)
            {
                Type YType = typeof(Y);
                MethodInfo methodType = methodSaved.MakeGenericMethod(YType);
                object? result = methodType.Invoke(this, parameters);
                if (result is Task<X?> task)
                {
                    return task;
                }
                return Task.FromResult((X?)result);
            }
            bool mustThrow = false;
            parameters ??= Array.Empty<object>();
            List<Type> types = new();
            foreach (object param in parameters)
            {
                Type type = param.GetType();
                if (param is Expression exp && type.IsGenericType)
                {
                    Type[] t = exp.Type.GetGenericArguments();
                    Type fctType = t.Length switch
                    {
                        1 => typeof(Func<>),
                        2 => typeof(Func<,>),
                        _ => throw new NotImplementedException()
                    };
                    fctType = fctType.MakeGenericType(t);
                    type = typeof(Expression<>).MakeGenericType(fctType);
                }
                types.Add(type);
            }

            MethodInfo[] methods = this.GetType().GetMethods();
            foreach (MethodInfo method in methods)
            {
                if (method.Name == name && method.IsGenericMethod)
                {
                    try
                    {
                        Type YType = typeof(Y);
                        MethodInfo methodType = method.MakeGenericMethod(YType);
                        mustThrow = true;
                        // it ll fail if Generic constraint are different but we can't deal it properly inside code so let the compiler do the job
                        if (checkSameParam)
                        {
                            if (GenericDM<T, U>.IsSameParameters(methodType.GetParameters(), types))
                            {
                                object? result = methodType.Invoke(this, parameters);
                                methodSaved = method;
                                if (result is Task<X?> task)
                                {
                                    return task;
                                }
                                return Task.FromResult((X?)result);
                            }
                        }
                        else
                        {
                            object? result = methodType.Invoke(this, parameters);
                            methodSaved = method;
                            if (result is Task<X?> task)
                            {
                                return task;
                            }
                            return Task.FromResult((X?)result);
                        }
                    }
                    catch (Exception e)
                    {
                        if (mustThrow)
                        {
                            PrintErrors(e);
#pragma warning disable CA2200 // Rethrow to preserve stack details
                            throw e;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                        }
                    }
                }
            }

            throw new DataError(DataErrorCode.MethodNotFound, "The method " + name + "(" + string.Join(", ", parameters.Select(p => p.GetType().Name)) + ") can't be found or failed").GetException();
        }

        protected X? InvokeMethod<X>(Type YType, object[]? parameters = null, bool checkSameParam = true, [CallerMemberName] string name = "")
        {
            bool mustThrow = false;
            parameters ??= Array.Empty<object>();
            List<Type> types = new();
            foreach (object param in parameters)
            {
                Type type = param.GetType();
                if (param is Expression exp && type.IsGenericType)
                {
                    Type[] t = exp.Type.GetGenericArguments();
                    Type fctType = t.Length switch
                    {
                        1 => typeof(Func<>),
                        2 => typeof(Func<,>),
                        _ => throw new NotImplementedException()
                    };
                    fctType = fctType.MakeGenericType(t);
                    type = typeof(Expression<>).MakeGenericType(fctType);
                }
                types.Add(type);
            }

            MethodInfo[] methods = this.GetType().GetMethods();
            foreach (MethodInfo method in methods)
            {
                if (method.Name == name && method.IsGenericMethod)
                {
                    try
                    {
                        MethodInfo methodType = method.MakeGenericMethod(YType);
                        // it ll fail if Generic constraint are different but we can't deal it properly inside code so let the compiler do the job
                        mustThrow = true;
                        if (checkSameParam)
                        {
                            if (GenericDM<T, U>.IsSameParameters(methodType.GetParameters(), types))
                            {
                                X? result = (X?)methodType.Invoke(this, parameters);
                                return result;
                            }
                        }
                        else
                        {
                            X? result = (X?)methodType.Invoke(this, parameters);
                            return result;
                        }
                    }
                    catch (Exception e)
                    {
                        if (mustThrow)
                        {
                            PrintErrors(e);
#pragma warning disable CA2200 // Rethrow to preserve stack details
                            throw e;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                        }
                    }
                }
            }

            throw new DataError(DataErrorCode.MethodNotFound, "The method " + name + "(" + string.Join(", ", parameters.Select(p => p.GetType().Name)) + ") can't be found").GetException();
        }
        protected Task<X?> InvokeMethodAsync<X>(Type YType, object[]? parameters = null, bool checkSameParam = true, [CallerMemberName] string name = "")
        {
            bool mustThrow = false;
            parameters ??= Array.Empty<object>();
            List<Type> types = new();
            foreach (object param in parameters)
            {
                Type type = param.GetType();
                if (param is Expression exp && type.IsGenericType)
                {
                    Type[] t = exp.Type.GetGenericArguments();
                    Type fctType = t.Length switch
                    {
                        1 => typeof(Func<>),
                        2 => typeof(Func<,>),
                        _ => throw new NotImplementedException()
                    };
                    fctType = fctType.MakeGenericType(t);
                    type = typeof(Expression<>).MakeGenericType(fctType);
                }
                types.Add(type);
            }

            MethodInfo[] methods = this.GetType().GetMethods();
            foreach (MethodInfo method in methods)
            {
                if (method.Name == name && method.IsGenericMethod)
                {
                    try
                    {
                        MethodInfo methodType = method.MakeGenericMethod(YType);
                        // it ll fail if Generic constraint are different but we can't deal it properly inside code so let the compiler do the job
                        mustThrow = true;
                        if (checkSameParam)
                        {
                            if (GenericDM<T, U>.IsSameParameters(methodType.GetParameters(), types))
                            {
                                object? result = methodType.Invoke(this, parameters);
                                if (result is Task<X?> task)
                                {
                                    return task;
                                }
                                return Task.FromResult((X?)result);
                            }
                        }
                        else
                        {
                            object? result = methodType.Invoke(this, parameters);
                            if (result is Task<X?> task)
                            {
                                return task;
                            }
                            return Task.FromResult((X?)result);
                        }
                    }
                    catch (Exception e)
                    {
                        if (mustThrow)
                        {
                            PrintErrors(e);
#pragma warning disable CA2200 // Rethrow to preserve stack details
                            throw e;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                        }
                    }
                }
            }

            throw new DataError(DataErrorCode.MethodNotFound, "The method " + name + "(" + string.Join(", ", parameters.Select(p => p.GetType().Name)) + ") can't be found").GetException();
        }


        protected void InvokeMethodVoid<Y>(ref MethodInfo? methodSaved, object[]? parameters = null, bool checkSameParam = true, [CallerMemberName] string name = "")
        {
            if (methodSaved != null)
            {
                Type YType = typeof(Y);
                MethodInfo methodType = methodSaved.MakeGenericMethod(YType);
                methodType.Invoke(this, parameters);
                return;
            }
            bool mustThrow = false;
            parameters ??= Array.Empty<object>();
            List<Type> types = new();
            foreach (object param in parameters)
            {
                Type type = param.GetType();
                if (param is Expression exp && type.IsGenericType)
                {
                    Type[] t = exp.Type.GetGenericArguments();
                    Type fctType = t.Length switch
                    {
                        1 => typeof(Func<>),
                        2 => typeof(Func<,>),
                        _ => throw new NotImplementedException()
                    };
                    fctType = fctType.MakeGenericType(t);
                    type = typeof(Expression<>).MakeGenericType(fctType);
                }
                types.Add(type);
            }

            MethodInfo[] methods = this.GetType().GetMethods();
            foreach (MethodInfo method in methods)
            {
                if (method.Name == name && method.IsGenericMethod)
                {
                    try
                    {
                        Type YType = typeof(Y);
                        MethodInfo methodType = method.MakeGenericMethod(YType);
                        // it ll fail if Generic constraint are different but we can't deal it properly inside code so let the compiler do the job
                        mustThrow = true;
                        if (checkSameParam)
                        {
                            if (GenericDM<T, U>.IsSameParameters(methodType.GetParameters(), types))
                            {
                                object? result = methodType.Invoke(this, parameters);
                                methodSaved = method;
                                return;
                            }
                        }
                        else
                        {
                            object? result = methodType.Invoke(this, parameters);
                            methodSaved = method;
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        if (mustThrow)
                        {
                            PrintErrors(e);
#pragma warning disable CA2200 // Rethrow to preserve stack details
                            throw e;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                        }
                    }
                }
            }

            throw new DataError(DataErrorCode.MethodNotFound, "The method " + name + "(" + string.Join(", ", parameters.Select(p => p.GetType().Name)) + ") can't be found or failed").GetException();
        }
        protected Task InvokeMethodTask<Y>(ref MethodInfo? methodSaved, object[]? parameters = null, bool checkSameParam = true, [CallerMemberName] string name = "")
        {
            if (methodSaved != null)
            {
                Type YType = typeof(Y);
                MethodInfo methodType = methodSaved.MakeGenericMethod(YType);
                methodType.Invoke(this, parameters);
                return Task.CompletedTask;
            }
            bool mustThrow = false;
            parameters ??= Array.Empty<object>();
            List<Type> types = new();
            foreach (object param in parameters)
            {
                Type type = param.GetType();
                if (param is Expression exp && type.IsGenericType)
                {
                    Type[] t = exp.Type.GetGenericArguments();
                    Type fctType = t.Length switch
                    {
                        1 => typeof(Func<>),
                        2 => typeof(Func<,>),
                        _ => throw new NotImplementedException()
                    };
                    fctType = fctType.MakeGenericType(t);
                    type = typeof(Expression<>).MakeGenericType(fctType);
                }
                types.Add(type);
            }

            MethodInfo[] methods = this.GetType().GetMethods();
            foreach (MethodInfo method in methods)
            {
                if (method.Name == name && method.IsGenericMethod)
                {
                    try
                    {
                        Type YType = typeof(Y);
                        MethodInfo methodType = method.MakeGenericMethod(YType);
                        // it ll fail if Generic constraint are different but we can't deal it properly inside code so let the compiler do the job
                        mustThrow = true;
                        if (checkSameParam)
                        {
                            if (GenericDM<T, U>.IsSameParameters(methodType.GetParameters(), types))
                            {
                                object? result = methodType.Invoke(this, parameters);
                                methodSaved = method;
                                if (result is Task task)
                                {
                                    return task;
                                }
                                return Task.CompletedTask;
                            }
                        }
                        else
                        {
                            object? result = methodType.Invoke(this, parameters);
                            methodSaved = method;
                            if (result is Task task)
                            {
                                return task;
                            }
                            return Task.CompletedTask;
                        }
                    }
                    catch (Exception e)
                    {
                        if (mustThrow)
                        {
                            PrintErrors(e);
#pragma warning disable CA2200 // Rethrow to preserve stack details
                            throw e;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                        }
                    }
                }
            }

            throw new DataError(DataErrorCode.MethodNotFound, "The method " + name + "(" + string.Join(", ", parameters.Select(p => p.GetType().Name)) + ") can't be found or failed").GetException();
        }

        private static bool IsSameParameters(ParameterInfo[] parameterInfos, List<Type> types)
        {
            if (parameterInfos.Length == types.Count)
            {
                for (int i = 0; i < parameterInfos.Length; i++)
                {
                    Type paramType = parameterInfos[i].ParameterType;
                    if (paramType.IsInterface)
                    {
                        if (!types[i].GetInterfaces().Contains(paramType))
                        {
                            return false;
                        }
                    }

                    else if (!parameterInfos[i].ParameterType.IsAssignableFrom(types[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }


        internal void PrintErrors(IWithError withError)
        {
            if (printErrorInConsole)
            {
                withError.Print();
            }
        }

        internal void PrintErrors(Exception e)
        {
            if (printErrorInConsole)
            {
                AventusLogger.Instance.LogError(exception: e, message: "One of Invoke method can't be done");
            }
        }

        void IGenericDM.PrintErrors(IWithError withError)
        {
            PrintErrors(withError);
        }

        #endregion

    }
}
