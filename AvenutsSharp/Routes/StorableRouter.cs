using AventusSharp.Data;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Response;
using AventusSharp.Routes.Request;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AventusSharp.Routes
{
    public abstract class StorableRouter<T, U, R, S> : IRouter
        where T : IStorable
        where U : Request.Request
        where R : Resource
        where S : Resource
    {
        /// <summary>
        /// Define the name of the object for the url
        /// </summary>
        /// <returns></returns>
        protected virtual string StorableName()
        {
            Type t = typeof(T);
            string name = t.Name;
            if (t.IsInterface)
            {
                if (name.StartsWith("I"))
                {
                    return name.Substring(1);
                }
            }
            return name;
        }


        [Get, Path("/[StorableName]")]
        public virtual async Task<ResultWithError<List<R>>> GetAll(HttpContext context)
        {
            ResultWithError<List<R>> result = new();
            List<T>? list = await result.ExtractAsync(() => DM_GetAll(context));
            if (list != null)
            {
                result.Result = new();
                foreach (T item in list)
                {
                    result.Result.Add(ConvertResource(context, item));
                }
            }
            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_GetAll(HttpContext context)
        {
            return (await Storable<T>.GetAllWithError()).ToGeneric();
        }

        [Post, Path("/[StorableName]")]
        public virtual async Task<ResultWithError<S>> Create(HttpContext context, U request)
        {
            T item = ConvertRequest(context, request);
            ResultWithError<S> result = new ResultWithError<S>();
            T? temp = await result.ExtractAsync(() => DM_Create(context, item));
            if (temp != null)
            {
                result.Result = ConvertResourceDetails(context, temp);
            }
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Create(HttpContext context, T item)
        {
            return (await Storable<T>.CreateWithError(item)).ToGeneric();
        }

        [Post, Path("/[StorableName]s")]
        public virtual async Task<ResultWithError<List<S>>> CreateMany(HttpContext context, List<U> list)
        {
            List<T> _list = new();
            foreach (U item in list)
            {
                _list.Add(ConvertRequest(context, item));
            }
            ResultWithError<List<S>> result = new();
            List<T>? listTemp = await result.ExtractAsync(() => DM_CreateMany(context, _list));
            if (listTemp != null)
            {
                result.Result = new();
                foreach (T item in listTemp)
                {
                    result.Result.Add(ConvertResourceDetails(context, item));
                }
            }

            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_CreateMany(HttpContext context, List<T> list)
        {
            return (await Storable<T>.CreateWithError(list)).ToGeneric();
        }

        [Get, Path("/[StorableName]/{id}")]
        public virtual async Task<ResultWithError<S>> GetById(HttpContext context, int id)
        {
            ResultWithError<S> result = new();
            T? item = await result.ExtractAsync(() => DM_GetById(context, id));
            if (item != null)
            {
                if (item.Id != id)
                {
                    AventusLogger.Instance.LogError("Impossible " + StorableName() + ": get " + item.Id + " instead of " + id);
                }
                result.Result = ConvertResourceDetails(context, item);
            }
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_GetById(HttpContext context, int id)
        {
            return (await Storable<T>.GetByIdWithError(id)).ToGeneric();
        }

        [Post, Path("/[StorableName]/getbyids")]
        public virtual async Task<ResultWithError<List<S>>> GetByIds(HttpContext context, List<int> ids)
        {
            ResultWithError<List<S>> result = new();
            List<T>? listTemp = await result.ExtractAsync(() => DM_GetByIds(context, ids));
            if (listTemp != null)
            {
                List<S> list = new();
                foreach (T item in listTemp)
                {
                    list.Add(ConvertResourceDetails(context, item));
                }
                result.Result = list;
            }
            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_GetByIds(HttpContext context, List<int> ids)
        {
            return (await Storable<T>.GetByIdsWithError(ids)).ToGeneric();
        }

        // [Post, Path("/[StorableName]/search")]
        // public virtual async Task<ResultWithError<List<T>>> Search(HttpContext context, string search, List<string> fields, int limit = -1, int page = 0)
        // {
        //     ResultWithError<List<T>> result = await DM_Search(context, search, fields, limit, page);
        //     if (result.Result != null)
        //     {
        //         List<T> list = new();
        //         foreach (T item in result.Result)
        //         {
        //             list.Add(OnSend(context, item));
        //         }
        //         result.Result = list;
        //     }
        //     return result;
        // }
        // protected virtual async Task<ResultWithError<List<T>>> DM_Search(HttpContext context, string search, List<string> fields, int limit, int page)
        // {
        //     var query = Storable<T>.StartQuery().Where(search, fields).Take(limit, limit * page);
        //     return await query.RunWithError();
        // }


        [Put]
        [Path("/[StorableName]/{id}")]
        public virtual async Task<ResultWithError<S>> Update(HttpContext context, int id, U request)
        {
            ResultWithError<S> result = new();
            T item = ConvertRequest(context, request);
            item.Id = id;
            T? temp = await result.ExtractAsync(() => DM_Update(context, item));
            if (temp != null)
            {
                result.Result = ConvertResourceDetails(context, temp);
            }
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Update(HttpContext context, T item)
        {
            return (await Storable<T>.UpdateWithError(item)).ToGeneric();
        }

        [Put]
        [Path("/[StorableName]s")]
        public virtual async Task<ResultWithError<List<S>>> UpdateMany(HttpContext context, List<U> list)
        {
            ResultWithError<List<S>> result = new();
            List<T> _list = new();
            foreach (U item in list)
            {
                _list.Add(ConvertRequest(context, item));
            }
            List<T>? listTemp = await result.ExtractAsync(() => DM_UpdateMany(context, _list));
            if (listTemp != null)
            {
                result.Result = new List<S>();
                foreach (T item in listTemp)
                {
                    result.Result.Add(ConvertResourceDetails(context, item));
                }
            }

            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_UpdateMany(HttpContext context, List<T> list)
        {
            return (await Storable<T>.UpdateWithError(list)).ToGeneric();
        }

        [Delete, Path("/[StorableName]/{id}")]
        public virtual async Task<ResultWithError<bool>> Delete(HttpContext context, int id)
        {
            ResultWithError<bool> result = new();
            await result.RunAsync(() => DM_Delete(context, id));
            result.Result = result.Success;
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Delete(HttpContext context, int id)
        {
            return (await Storable<T>.DeleteWithError(id)).ToGeneric();
        }

        [Delete, Path("/[StorableName]s")]
        public virtual async Task<ResultWithError<bool>> DeleteMany(HttpContext context, List<int> ids)
        {
            ResultWithError<bool> result = new();
            await result.RunAsync(() => DM_DeleteMany(context, ids));
            result.Result = result.Success;
            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_DeleteMany(HttpContext context, List<int> ids)
        {
            return (await Storable<T>.DeleteWithError(ids)).ToGeneric();
        }

        protected virtual T ConvertRequest(HttpContext context, U item)
        {
            return Binder.AutoBind<T>(item);
        }
        protected virtual R ConvertResource(HttpContext context, T item)
        {
            return Binder.AutoBind<R>(item);
        }
        protected virtual S ConvertResourceDetails(HttpContext context, T item)
        {
            return Binder.AutoBind<S>(item);
        }

    }

    public abstract class StorableRouter<T, U, R> : StorableRouter<T, U, R, R>
       where T : IStorable
       where U : Request.Request
       where R : Resource
    {
        protected override sealed R ConvertResourceDetails(HttpContext context, T item)
        {
            return ConvertResource(context, item);
        }
    }
    
    public abstract class StorableRouter<T> : IRouter where T : IStorable
    {
        /// <summary>
        /// Define the name of the object for the url
        /// </summary>
        /// <returns></returns>
        protected virtual string StorableName()
        {
            Type t = typeof(T);
            string name = t.Name;
            if (t.IsInterface)
            {
                if (name.StartsWith("I"))
                {
                    return name.Substring(1);
                }
            }
            return name;
        }


        [Get, Path("/[StorableName]")]
        public virtual async Task<ResultWithError<List<T>>> GetAll(HttpContext context)
        {
            ResultWithError<List<T>> result = await DM_GetAll(context);
            if (result.Result != null)
            {
                List<T> list = new();
                foreach (T item in result.Result)
                {
                    list.Add(OnSend(context, item));
                }
                result.Result = list;
            }
            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_GetAll(HttpContext context)
        {
            return (await Storable<T>.GetAllWithError()).ToGeneric();
        }

        [Post, Path("/[StorableName]")]
        public virtual async Task<ResultWithError<T>> Create(HttpContext context, T item)
        {
            item = OnReceive(context, item);
            ResultWithError<T> result = await DM_Create(context, item);
            if (result.Result != null)
            {
                result.Result = OnSend(context, item);
            }
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Create(HttpContext context, T item)
        {
            return (await Storable<T>.CreateWithError(item)).ToGeneric();
        }

        [Post, Path("/[StorableName]s")]
        public virtual async Task<ResultWithError<List<T>>> CreateMany(HttpContext context, List<T> list)
        {
            List<T> _list = new();
            foreach (T item in list)
            {
                _list.Add(OnReceive(context, item));
            }
            ResultWithError<List<T>> result = await DM_CreateMany(context, _list);
            if (result.Result != null)
            {
                List<T> listTemp = new();
                foreach (T item in result.Result)
                {
                    listTemp.Add(OnSend(context, item));
                }
                result.Result = listTemp;
            }

            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_CreateMany(HttpContext context, List<T> list)
        {
            return (await Storable<T>.CreateWithError(list)).ToGeneric();
        }

        [Get, Path("/[StorableName]/{id}")]
        public virtual async Task<ResultWithError<T>> GetById(HttpContext context, int id)
        {
            ResultWithError<T> result = await DM_GetById(context, id);
            if (result.Result != null)
            {
                if (result.Result.Id != id)
                {
                    AventusLogger.Instance.LogError("Impossible " + StorableName() + ": get " + result.Result.Id + " instead of " + id);
                }
                result.Result = OnSend(context, result.Result);
            }
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_GetById(HttpContext context, int id)
        {
            return (await Storable<T>.GetByIdWithError(id)).ToGeneric();
        }

        [Post, Path("/[StorableName]/getbyids")]
        public virtual async Task<ResultWithError<List<T>>> GetByIds(HttpContext context, List<int> ids)
        {
            ResultWithError<List<T>> result = await DM_GetByIds(context, ids);
            if (result.Result != null)
            {
                List<T> list = new();
                foreach (T item in result.Result)
                {
                    list.Add(OnSend(context, item));
                }
                result.Result = list;
            }
            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_GetByIds(HttpContext context, List<int> ids)
        {
            return (await Storable<T>.GetByIdsWithError(ids)).ToGeneric();
        }

        [Post, Path("/[StorableName]/search")]
        public virtual async Task<ResultWithError<List<T>>> Search(HttpContext context, string search, List<string> fields, int limit = -1, int page = 0)
        {
            ResultWithError<List<T>> result = await DM_Search(context, search, fields, limit, page);
            if (result.Result != null)
            {
                List<T> list = new();
                foreach (T item in result.Result)
                {
                    list.Add(OnSend(context, item));
                }
                result.Result = list;
            }
            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_Search(HttpContext context, string search, List<string> fields, int limit, int page)
        {
            var query = Storable<T>.StartQuery().Where(search, fields).Take(limit, limit * page);
            return await query.RunWithError();
        }


        [Put]
        [Path("/[StorableName]/{id}")]
        public virtual async Task<ResultWithError<T>> Update(HttpContext context, int id, T item)
        {
            item.Id = id;
            item = OnReceive(context, item);
            ResultWithError<T> result = await DM_Update(context, item);
            if (result.Result != null)
            {
                result.Result = OnSend(context, item);
            }
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Update(HttpContext context, T item)
        {
            return (await Storable<T>.UpdateWithError(item)).ToGeneric();
        }

        [Put]
        [Path("/[StorableName]s")]
        public virtual async Task<ResultWithError<List<T>>> UpdateMany(HttpContext context, List<T> list)
        {
            List<T> _list = new();
            foreach (T item in list)
            {
                _list.Add(OnReceive(context, item));
            }
            ResultWithError<List<T>> result = await DM_UpdateMany(context, _list);
            if (result.Result != null)
            {
                List<T> listTemp = new();
                foreach (T item in result.Result)
                {
                    listTemp.Add(OnSend(context, item));
                }
                result.Result = listTemp;
            }

            return result;
        }

        protected virtual async Task<ResultWithError<List<T>>> DM_UpdateMany(HttpContext context, List<T> list)
        {
            return (await Storable<T>.UpdateWithError(list)).ToGeneric();
        }

        [Delete, Path("/[StorableName]/{id}")]
        public virtual async Task<ResultWithError<T>> Delete(HttpContext context, int id)
        {
            ResultWithError<T> result = await DM_Delete(context, id);
            if (result.Result != null)
            {
                result.Result = OnSend(context, result.Result);
            }
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Delete(HttpContext context, int id)
        {
            return (await Storable<T>.DeleteWithError(id)).ToGeneric();
        }

        [Delete, Path("/[StorableName]s")]
        public virtual async Task<ResultWithError<List<T>>> DeleteMany(HttpContext context, List<int> ids)
        {
            ResultWithError<List<T>> result = await DM_DeleteMany(context, ids);
            if (result.Result != null)
            {
                List<T> listTemp = new();
                foreach (T item in result.Result)
                {
                    listTemp.Add(OnSend(context, item));
                }
                result.Result = listTemp;
            }

            return result;
        }

        protected virtual async Task<ResultWithError<List<T>>> DM_DeleteMany(HttpContext context, List<int> ids)
        {
            return (await Storable<T>.DeleteWithError(ids)).ToGeneric();
        }

        protected virtual T OnReceive(HttpContext context, T item)
        {
            return item;
        }
        protected virtual T OnSend(HttpContext context, T item)
        {
            return item;
        }
    }
}
