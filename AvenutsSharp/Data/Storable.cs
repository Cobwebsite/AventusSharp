using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AventusSharp.Data
{
    public class Storable
    {
        public static readonly string Id = "Id";
        internal Dictionary<Type, IDBStorage> storageByClass = new();
    }
    public interface IStorable
    {
        int Id { get; set; }

        public List<DataError> IsValid(StorableAction action);

        Task<bool> Create();
        public Task<List<GenericError>> CreateWithError();

        Task<bool> Update();
        public Task<List<GenericError>> UpdateWithError();

        public Task<bool> Delete();
        public Task<List<GenericError>> DeleteWithError();
    }

    public interface IStorableTimestamp : IStorable
    {
        DateTime CreatedDate { get; set; }
        DateTime UpdatedDate { get; set; }
    }

    [ForceInherit]
    [NoExport]
    public abstract class StorableTimestamp<T> : Storable<T>, IStorableTimestamp where T : IStorableTimestamp
    {
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }

    [ForceInherit]
    [NoExport]
    public abstract class Storable<T> : IStorable where T : IStorable
    {
        public static T OnlyId(int id)
        {
            T el = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            el.Id = id;
            return el;
        }
        public static void EnableDebug()
        {
            var st = DBStorage.GetFrom<T>();
            if (st != null)
            {
                st.Debug = true;
            }
        }
        public static void DisableDebug()
        {
            var st = DBStorage.GetFrom<T>();
            if (st != null)
            {
                st.Debug = true;
            }
        }

        [Primary, AutoIncrement]
        public virtual int Id { get; set; }

        public static Task<List<T>> GetAll()
        {
            return GenericDM.Get<T>().GetAll<T>();
        }
        public static Task<ResultWithError<List<T>>> GetAllWithError()
        {
            return GenericDM.Get<T>().GetAllWithError<T>();
        }
        public static IQueryBuilder<T> StartQuery()
        {
            return GenericDM.Get<T>().CreateQuery<T>();
        }
        public static ICreateBuilder<T> StartCreate()
        {
            return GenericDM.Get<T>().CreateCreate<T>();
        }
        public static IUpdateBuilder<T> StartUpdate()
        {
            return GenericDM.Get<T>().CreateUpdate<T>();
        }
        public static IDeleteBuilder<T> StartDelete()
        {
            return GenericDM.Get<T>().CreateDelete<T>();
        }
        public static IExistBuilder<T> StartExist()
        {
            return GenericDM.Get<T>().CreateExist<T>();
        }

        public static Task<T?> GetById(int id)
        {
            return GenericDM.Get<T>().GetById<T>(id);
        }
        public static Task<ResultWithError<T>> GetByIdWithError(int id)
        {
            return GenericDM.Get<T>().GetByIdWithError<T>(id);
        }
        public static Task<List<T>> GetByIds(List<int> ids)
        {
            return GenericDM.Get<T>().GetByIds<T>(ids);
        }
        public static Task<List<T>> GetByIds(params int[] ids)
        {
            return GenericDM.Get<T>().GetByIds<T>(ids.ToList());
        }
        public static Task<ResultWithError<List<T>>> GetByIdsWithError(List<int> ids)
        {
            return GenericDM.Get<T>().GetByIdsWithError<T>(ids);
        }
        public static Task<ResultWithError<List<T>>> GetByIdsWithError(params int[] ids)
        {
            return GenericDM.Get<T>().GetByIdsWithError<T>(ids.ToList());
        }


        public static Task<List<T>> Where(Expression<Func<T, bool>> func)
        {
            return GenericDM.Get<T>().Where(func);
        }
        public static Task<ResultWithError<List<T>>> WhereWithError(Expression<Func<T, bool>> func)
        {
            return GenericDM.Get<T>().WhereWithError(func);
        }

        public static Task<T?> Single(Expression<Func<T, bool>> func)
        {
            return GenericDM.Get<T>().Single(func);
        }
        public static Task<ResultWithError<T>> SingleWithError(Expression<Func<T, bool>> func)
        {
            return GenericDM.Get<T>().SingleWithError(func);
        }

        public static Task<bool> Exist(Expression<Func<T, bool>> func)
        {
            return GenericDM.Get<T>().Exist(func);
        }
        public static Task<ResultWithError<bool>> ExistWithError(Expression<Func<T, bool>> func)
        {
            return GenericDM.Get<T>().ExistWithError(func);
        }

        #region Create
        /// <summary>
        /// Create inside the DM a bunch of elements and return them
        /// If something went wrong an empty list will be returned
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static async Task<List<T>> Create(List<T> values)
        {
            if (values != null && values.Count > 0)
            {
                return await GenericDM.Get<T>().Create(values);
            }
            return new List<T>();
        }
        /// <summary>
        /// Create inside the DM a bunch of elements and return them
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static async Task<ResultWithError<List<T>>> CreateWithError(List<T> values)
        {
            if (values != null && values.Count > 0)
            {
                return await GenericDM.Get<T>().CreateWithError(values);
            }

            ResultWithError<List<T>> result = new();
            // result.Errors.Add(new DataError(DataErrorCode.NoItemProvided, "You must provide values to create"));
            result.Result = new List<T>();
            return result;
        }
        /// <summary>
        /// Create inside the DM a bunch of elements and return them
        /// If something went wrong an empty list will be returned
        /// </summary>
        /// <param name="values"></param>
        /// <param name="withId"></param>
        /// <returns></returns>
        public static async Task<bool> BulkCreate(List<T> values, bool withId = false)
        {
            // TODO change withId by a config object to add bufferSize
            if (values != null && values.Count > 0)
            {
                return await GenericDM.Get<T>().BulkCreate(values, withId);
            }
            return true;
        }
        /// <summary>
        /// Create inside the DM a bunch of elements and return them
        /// </summary>
        /// <param name="values"></param>
        /// <param name="withId"></param>
        /// <returns></returns>
        public static async Task<VoidWithError> BulkCreateWithError(List<T> values, bool withId = false)
        {
            if (values != null && values.Count > 0)
            {
                return await GenericDM.Get<T>().BulkCreateWithError(values, withId);
            }
            return new();
        }
        /// <summary>
        /// Create the value inside the DM and return it
        /// If something went wrong a null is returned
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task<T?> Create(T value)
        {
            if (value != null)
            {
                return await GenericDM.Get<T>().Create(value);
            }
            return default;
        }
        /// <summary>
        /// Create the value inside the DM and return it
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task<ResultWithError<T>> CreateWithError(T value)
        {
            return await GenericDM.Get<T>().CreateWithError(value);
        }
        /// <summary>
        /// Create the current element inside the DM
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Create()
        {
            return (await CreateWithError()).Count == 0;
        }
        /// <summary>
        /// Create the current element inside the DM
        /// If return Count == 0 it means no error and your item is stored
        /// </summary>
        /// <returns></returns>
        public async Task<List<GenericError>> CreateWithError()
        {
            if (this is T TThis)
            {
                ResultWithError<T> result = await GenericDM.Get<T>().CreateWithError(TThis);
                if (result.Success)
                {
                    if (Equals(result.Result, this))
                    {
                        return new List<GenericError>();
                    }
                    return new List<GenericError>() { new DataError(DataErrorCode.UnknowError, "Element is overrided => impossible") };
                }
                return result.Errors;

            }
            string errorMsg = "Element " + this.GetType() + " isn't a " + typeof(T).Name + ". This should be impossible";
            DataError error = new(DataErrorCode.WrongType, errorMsg);
            return new List<GenericError>() { error };
        }
        #endregion

        #region Update
        /// <summary>
        /// Update inside the DM a bunch of elements and return them
        /// If something went wrong an empty list will be returned
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static async Task<List<T>> Update(List<T> values)
        {
            if (values != null && values.Count > 0)
            {
                return await GenericDM.Get<T>().Update(values);
            }
            return new List<T>();
        }
        /// <summary>
        /// Update inside the DM a bunch of elements and return them
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static async Task<ResultWithError<List<T>>> UpdateWithError(List<T> values)
        {
            if (values != null && values.Count > 0)
            {
                return await GenericDM.Get<T>().UpdateWithError(values);
            }

            ResultWithError<List<T>> result = new();
            // result.Errors.Add(new DataError(DataErrorCode.NoItemProvided, "You must provide values to Update"));
            result.Result = new List<T>();
            return result;
        }
        /// <summary>
        /// Update the value inside the DM and return it
        /// If something went wrong a null is returned
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task<T?> Update(T value)
        {
            if (value != null)
            {
                return await GenericDM.Get<T>().Update(value);
            }
            return default;
        }
        /// <summary>
        /// Update the value inside the DM and return it
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task<ResultWithError<T>> UpdateWithError(T value)
        {
            return await GenericDM.Get<T>().UpdateWithError(value);
        }
        /// <summary>
        /// Update the current element inside the DM
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Update()
        {
            return (await UpdateWithError()).Count == 0;
        }

        /// <summary>
        /// Update the current element inside the DM
        /// If return Count == 0 it means no error and your item is stored
        /// </summary>
        /// <returns></returns>
        public async Task<List<GenericError>> UpdateWithError()
        {
            if (this is T TThis)
            {
                ResultWithError<T> result = await GenericDM.Get<T>().UpdateWithError(TThis);
                if (result.Success)
                {
                    if (Equals(result.Result, this))
                    {
                        return result.Errors;
                    }
                    return new List<GenericError>() { new DataError(DataErrorCode.UnknowError, "Element is overrided => impossible") };
                }
                return result.Errors;
            }
            string errorMsg = "Element " + this.GetType() + " isn't a " + typeof(T).Name + ". This should be impossible";
            DataError error = new(DataErrorCode.WrongType, errorMsg);
            return new List<GenericError>() { error };
        }
        #endregion

        #region Delete
        /// <summary>
        /// Delete inside the DM a bunch of elements and return them
        /// If something went wrong an empty list will be returned
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static async Task<List<T>> Delete(List<T> values)
        {
            if (values != null && values.Count > 0)
            {
                return await GenericDM.Get<T>().Delete(values);
            }
            return new List<T>();
        }
        /// <summary>
        /// Delete inside the DM a bunch of elements and return them
        /// If something went wrong an empty list will be returned
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public static async Task<List<T>> Delete(List<int> ids)
        {
            if (ids != null && ids.Count > 0)
            {
                ResultWithError<List<T>> resultTemp = await DeleteWithError(ids);
                if (resultTemp.Result != null)
                {
                    return resultTemp.Result;
                }
            }
            return new List<T>();
        }
        /// <summary>
        /// Delete inside the DM a bunch of elements and return them
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static async Task<ResultWithError<List<T>>> DeleteWithError(List<T> values)
        {
            if (values != null && values.Count > 0)
            {
                return await GenericDM.Get<T>().DeleteWithError(values);
            }

            ResultWithError<List<T>> result = new();
            // result.Errors.Add(new DataError(DataErrorCode.NoItemProvided, "You must provide values to Delete"));
            result.Result = new List<T>();
            return result;
        }
        /// <summary>
        /// Delete inside the DM a bunch of elements and return them
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public static async Task<ResultWithError<List<T>>> DeleteWithError(List<int> ids)
        {
            if (ids != null && ids.Count > 0)
            {
                ResultWithError<List<T>> resultTemp = await GenericDM.Get<T>().GetByIdsWithError<T>(ids);
                if (resultTemp.Success && resultTemp.Result != null)
                {
                    return await GenericDM.Get<T>().DeleteWithError(resultTemp.Result);
                }
                return resultTemp;
            }

            ResultWithError<List<T>> result = new();
            // result.Errors.Add(new DataError(DataErrorCode.NoItemProvided, "You must provide values to Delete"));
            result.Result = new List<T>();
            return result;
        }
        /// <summary>
        /// Delete the value inside the DM and return it
        /// If something went wrong a null is returned
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task<T?> Delete(T value)
        {
            if (value != null)
            {
                return await GenericDM.Get<T>().Delete(value);
            }
            return default;
        }

        public static async Task<T?> Delete(int id)
        {
            ResultWithError<T> resultTemp = await DeleteWithError(id);
            if (resultTemp.Success && resultTemp.Result != null)
            {
                return resultTemp.Result;
            }
            return default;
        }

        public static async Task<ResultWithError<T>> DeleteWithError(int id)
        {
            ResultWithError<T> resultTemp = await GenericDM.Get<T>().GetByIdWithError<T>(id);
            if (resultTemp.Success && resultTemp.Result != null)
            {
                resultTemp.Errors = await resultTemp.Result.DeleteWithError();
            }
            return resultTemp;
        }
        /// <summary>
        /// Delete the value inside the DM and return it
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task<ResultWithError<T>> DeleteWithError(T value)
        {
            return await GenericDM.Get<T>().DeleteWithError(value);
        }

        /// <summary>
        /// Delete the current element inside the DM
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Delete()
        {
            return (await DeleteWithError()).Count == 0;
        }
        /// <summary>
        /// Delete the current element inside the DM
        /// If return Count == 0 it means no error and your item is stored
        /// </summary>
        /// <returns></returns>
        public async Task<List<GenericError>> DeleteWithError()
        {
            if (this is T TThis)
            {
                ResultWithError<T> result = await GenericDM.Get<T>().DeleteWithError(TThis);
                return result.Errors;
            }
            string errorMsg = "Element " + this.GetType() + " isn't a " + typeof(T).Name + ". This should be impossible";
            DataError error = new(DataErrorCode.WrongType, errorMsg);
            return new List<GenericError>() { error };
        }
        #endregion

        /// <summary>
        /// Apply the function ValidationRules to check if element is valid
        /// </summary>
        /// <param name="action">The type of action that you need to check</param>
        /// <returns></returns>
        public List<DataError> IsValid(StorableAction action)
        {
            List<DataError> errors = new();
            errors.AddRange(ValidationRules(action));
            return errors;
        }
        /// <summary>
        /// Define custom rules you need to check for your object
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        protected virtual List<DataError> ValidationRules(StorableAction action)
        {
            return new List<DataError>();
        }

        #region Load Dependances

        /// <summary>
        /// Allow to load the a real object from an id on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="fct">The field with the int (Id of the element to load)</param>
        /// <param name="set">Set to add the object to the item</param>
        /// <returns></returns>
        public async Task<ResultWithError<Y>> LoadObjectFromId<Y>(Func<T, int> fct, Action<T, Y> set) where Y : IStorable
        {
            if (this is T t)
            {
                return await LoadObjectFromId(t, fct, set);
            }
            return new ResultWithError<Y>()
            {
                Errors = new()
                {
                    new DataError(DataErrorCode.WrongType, "Element " + GetType() + " isn't a " + typeof(T).Name + ". This should be impossible")
                }
            };
        }

        /// <summary>
        /// Allow to load the a real object from an id on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="from">Items to augment</param>
        /// <param name="fct">The field with the int (Id of the element to load)</param>
        /// <param name="set">Set to add the object to the item</param>
        /// <returns></returns>
        public static Task<ResultWithError<List<Y>>> LoadObjectFromId<Y>(List<T>? from, Func<T, int> fct, Action<T, Y> set) where Y : IStorable
        {
            ResultWithError<List<T>> realFrom = new ResultWithError<List<T>>() { Result = from };
            return LoadObjectFromId(realFrom, fct, set);
        }

        /// <summary>
        /// Allow to load the a real object from an id on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="from">Items to augment</param>
        /// <param name="fct">The field with the int (Id of the element to load)</param>
        /// <param name="set">Set to add the object to the item</param>
        /// <returns></returns>
        public static async Task<ResultWithError<Y>> LoadObjectFromId<Y>(T from, Func<T, int> fct, Action<T, Y> set) where Y : IStorable
        {
            ResultWithError<List<T>> realFrom = new ResultWithError<List<T>>() { Result = new List<T> { from } };
            ResultWithError<List<Y>> resultTemp = await LoadObjectFromId(realFrom, fct, set);
            ResultWithError<Y> result = new()
            {
                Result = resultTemp.Result != null && resultTemp.Result.Count > 0 ? resultTemp.Result[0] : default,
                Errors = resultTemp.Errors
            };
            return result;
        }

        /// <summary>
        /// Allow to load the a real object from an id on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="from">Items to augment</param>
        /// <param name="fct">The field with the int (Id of the element to load)</param>
        /// <param name="set">Set to add the object to the item</param>
        /// <returns></returns>
        public static async Task<ResultWithError<List<Y>>> LoadObjectFromId<Y>(ResultWithError<List<T>> from, Func<T, int> fct, Action<T, Y> set) where Y : IStorable
        {
            return await LoaderHelper.LoadObjectFromId(from, fct, set);
        }
        /// <summary>
        /// Allow to load the a real object from an id on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="from">Items to augment</param>
        /// <param name="fct">The field with the int (Id of the element to load)</param>
        /// <param name="set">Set to add the object to the item</param>
        /// <returns></returns>
        public static async Task<ResultWithError<Y>> LoadObjectFromId<Y>(ResultWithError<T> from, Func<T, int> fct, Action<T, Y> set) where Y : IStorable
        {
            ResultWithError<List<T>> realFrom = new ResultWithError<List<T>>()
            {
                Errors = from.Errors,
                Result = from.Result != null ? new List<T> { from.Result } : null
            };
            ResultWithError<List<Y>> resultTemp = await LoadObjectFromId(realFrom, fct, set);
            ResultWithError<Y> result = new()
            {
                Result = resultTemp.Result != null && resultTemp.Result.Count > 0 ? resultTemp.Result[0] : default,
                Errors = resultTemp.Errors
            };
            return result;
        }


        /// <summary>
        /// Allow to load the list of real object from a list of ids on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="fct">The field with the List of int</param>
        /// <param name="set">Set to add the object to the list</param>
        /// <returns></returns>
        public async Task<ResultWithError<List<Y>>> LoadObjectsFromIds<Y>(Func<T, List<int>> fct, Action<T, Y> set) where Y : IStorable
        {
            if (this is T t)
            {
                return await LoadObjectsFromIds(t, fct, set);
            }
            return new ResultWithError<List<Y>>()
            {
                Errors = new()
                {
                    new DataError(DataErrorCode.WrongType, "Element " + GetType() + " isn't a " + typeof(T).Name + ". This should be impossible")
                }
            };

        }
        /// <summary>
        /// Allow to load the list of real object from a list of ids on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="from">Items to augment</param>
        /// <param name="fct">The field with the List of int</param>
        /// <param name="set">Set to add the object to the list</param>
        /// <returns></returns>
        public static Task<ResultWithError<List<Y>>> LoadObjectsFromIds<Y>(List<T>? from, Func<T, List<int>> fct, Action<T, Y> set) where Y : IStorable
        {
            ResultWithError<List<T>> realFrom = new ResultWithError<List<T>>() { Result = from };
            return LoadObjectsFromIds(realFrom, fct, set);
        }
        /// <summary>
        /// Allow to load the list of real object from a list of ids on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="from">Items to augment</param>
        /// <param name="fct">The field with the List of int</param>
        /// <param name="set">Set to add the object to the list</param>
        /// <returns></returns>
        public static Task<ResultWithError<List<Y>>> LoadObjectsFromIds<Y>(T from, Func<T, List<int>> fct, Action<T, Y> set) where Y : IStorable
        {
            ResultWithError<List<T>> realFrom = new ResultWithError<List<T>>() { Result = new List<T> { from } };
            return LoadObjectsFromIds(realFrom, fct, set);
        }
        /// <summary>
        /// Allow to load the list of real object from a list of ids on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="from">Items to augment</param>
        /// <param name="fct">The field with the List of int</param>
        /// <param name="set">Set to add the object to the list</param>
        /// <returns></returns>
        public static async Task<ResultWithError<List<Y>>> LoadObjectsFromIds<Y>(ResultWithError<List<T>> from, Func<T, List<int>> fct, Action<T, Y> set) where Y : IStorable
        {
            return await LoaderHelper.LoadObjectsFromIds(from, fct, set);
        }
        /// <summary>
        /// Allow to load the list of real object from a list of ids on the same element
        /// </summary>
        /// <typeparam name="Y"></typeparam>
        /// <param name="from">Items to augment</param>
        /// <param name="fct">The field with the List of int</param>
        /// <param name="set">Set to add the object to the list</param>
        /// <returns></returns>
        public static async Task<ResultWithError<List<Y>>> LoadObjectsFromIds<Y>(ResultWithError<T> from, Func<T, List<int>> fct, Action<T, Y> set) where Y : IStorable
        {
            ResultWithError<List<T>> realFrom = new ResultWithError<List<T>>()
            {
                Errors = from.Errors,
                Result = from.Result != null ? new List<T> { from.Result } : null
            };
            return await LoaderHelper.LoadObjectsFromIds(realFrom, fct, set);
        }
        #endregion

        #region Load
        public Task<VoidWithError> Load(Expression<Func<T, object?>> expression)
        {
            return Load([expression]);
        }
        public async Task<VoidWithError> Load(List<Expression<Func<T, object?>>> expressions)
        {
            if (this is T that)
            {
                return await Load([that], expressions);
            }
            return new VoidWithError()
            {
                Errors = new()
                {
                    new DataError(DataErrorCode.WrongType, "Element " + GetType() + " isn't a " + typeof(T).Name + ". This should be impossible")
                }
            };
        }

        public static Task<VoidWithError> Load(List<T> from, Expression<Func<T, object?>> expression)
        {
            return Load(from, [expression]);
        }
        public static Task<VoidWithError> Load(List<T> from, List<Expression<Func<T, object?>>> expressions)
        {
            return Load(new ResultWithError<List<T>>() { Result = from }, expressions);
        }
        public static Task<VoidWithError> Load(ResultWithError<List<T>> from, Expression<Func<T, object?>> expression)
        {
            return Load(from, [expression]);
        }
        public static Task<VoidWithError> Load(ResultWithError<List<T>> from, List<Expression<Func<T, object?>>> expressions)
        {
            return LoaderHelper.LoadInternal(from, expressions);
        }
        #endregion
    }


}
