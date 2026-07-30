
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using MySqlX.XDevAPI.Common;

namespace AventusSharp.Data;

public static class ListStorable
{

    #region Create

    /// <summary>
    /// Create inside the DM a bunch of elements and return them
    /// If something went wrong an empty list will be returned
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static async Task<List<T>> Create<T>(this List<T> values) where T : IStorable
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
    public static async Task<ResultWithError<List<T>>> CreateWithError<T>(this List<T> values) where T : IStorable
    {
        if (values != null && values.Count > 0)
        {
            return await GenericDM.Get<T>().CreateWithError(values);
        }

        ResultWithError<List<T>> result = new()
        {
            Result = new List<T>()
        };
        return result;
    }

    /// <summary>
    /// Create inside the DM a bunch of elements and return them
    /// If something went wrong an empty list will be returned
    /// </summary>
    /// <param name="values"></param>
    /// <param name="withId"></param>
    /// <returns></returns>
    public static async Task<bool> BulkCreate<T>(this List<T> values, bool withId = false) where T : IStorable
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
    public static async Task<VoidWithError> BulkCreateWithError<T>(this List<T> values, bool withId = false) where T : IStorable
    {
        if (values != null && values.Count > 0)
        {
            return await GenericDM.Get<T>().BulkCreateWithError(values, withId);
        }
        return new();
    }

    #endregion

    #region Update
    /// <summary>
    /// Update inside the DM a bunch of elements and return them
    /// If something went wrong an empty list will be returned
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static async Task<List<T>> Update<T>(this List<T> values) where T : IStorable
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
    public static async Task<ResultWithError<List<T>>> UpdateWithError<T>(this List<T> values) where T : IStorable
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

    #endregion

    #region Delete
    /// <summary>
    /// Delete inside the DM a bunch of elements and return them
    /// If something went wrong an empty list will be returned
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static async Task<List<T>> Delete<T>(this List<T> values) where T : IStorable
    {
        if (values != null && values.Count > 0)
        {
            return await GenericDM.Get<T>().Delete(values);
        }
        return new List<T>();
    }

    /// <summary>
    /// Delete inside the DM a bunch of elements and return them
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static async Task<ResultWithError<List<T>>> DeleteWithError<T>(this List<T> values) where T : IStorable
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

    #endregion

    #region Load Dependencies
    /// <summary>
    /// Allow to load the a real object from an id on the same element
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Y"></typeparam>
    /// <param name="from">Items to augment</param>
    /// <param name="fct">The field with the int (Id of the element to load)</param>
    /// <param name="set">Set to add the object to the item</param>
    /// <returns></returns>
    public static Task<ResultWithError<List<Y>>> LoadObjectFromId<T, Y>(this List<T>? from, Func<T, int> fct, Action<T, Y> set) where T : IStorable where Y : IStorable
    {
        ResultWithError<List<T>> realFrom = new ResultWithError<List<T>>() { Result = from };
        return LoaderHelper.LoadObjectFromId(realFrom, fct, set);
    }

    /// <summary>
    /// Allow to load the list of real object from a list of ids on the same element
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Y"></typeparam>
    /// <param name="from">Items to augment</param>
    /// <param name="fct">The field with the List of int</param>
    /// <param name="set">Set to add the object to the list</param>
    /// <returns></returns>
    public static Task<ResultWithError<List<Y>>> LoadObjectsFromIds<T, Y>(this List<T>? from, Func<T, List<int>> fct, Action<T, Y> set) where T : IStorable where Y : IStorable
    {
        ResultWithError<List<T>> realFrom = new ResultWithError<List<T>>() { Result = from };
        return LoaderHelper.LoadObjectsFromIds(realFrom, fct, set);
    }
    #endregion

  
    #region Load
    public static Task<VoidWithError> Load<T>(this List<T> from, Expression<Func<T, object?>> expression) where T : IStorable
    {
        return from.Load([expression]);
    }
    public static Task<VoidWithError> Load<T>(this List<T> from, List<Expression<Func<T, object?>>> expressions) where T : IStorable
    {
        return Load(new ResultWithError<List<T>>() { Result = from }, expressions);
    }
    public static Task<VoidWithError> Load<T>(this ResultWithError<List<T>> from, Expression<Func<T, object?>> expression) where T : IStorable
    {
        return Load(from, [expression]);

    }
    public static Task<VoidWithError> Load<T>(this ResultWithError<List<T>> from, List<Expression<Func<T, object?>>> expressions) where T : IStorable
    {
        return LoaderHelper.LoadInternal(from, expressions);
    }
    #endregion
}