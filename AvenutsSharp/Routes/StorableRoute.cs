using AventusSharp.Data;
using AventusSharp.Routes.Attributes;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace AventusSharp.Routes
{
    public abstract class StorableRoute<T> : IRouter where T : IStorable
    {
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
        public virtual ResultWithError<List<T>> GetAll(HttpContext context)
        {
            ResultWithError<List<T>> result = DM_GetAll(context);
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
        protected virtual ResultWithError<List<T>> DM_GetAll(HttpContext context)
        {
            return Storable<T>.GetAllWithError().ToGeneric();
        }

        [Post, Path("/[StorableName]")]
        public virtual ResultWithError<T> Create(HttpContext context, T item)
        {
            item = OnReceive(context, item);
            ResultWithError<T> result = DM_Create(context, item);
            if (result.Result != null)
            {
                result.Result = OnSend(context, item);
            }
            return result;
        }
        protected virtual ResultWithError<T> DM_Create(HttpContext context, T item)
        {
            return Storable<T>.CreateWithError(item).ToGeneric();
        }

        [Post, Path("/[StorableName]s")]
        public virtual ResultWithError<List<T>> CreateMany(HttpContext context, List<T> list)
        {
            List<T> _list = new();
            foreach (T item in list)
            {
                _list.Add(OnReceive(context, item));
            }
            ResultWithError<List<T>> result = DM_CreateMany(context, _list);
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
        protected virtual ResultWithError<List<T>> DM_CreateMany(HttpContext context, List<T> list)
        {
            return Storable<T>.CreateWithError(list).ToGeneric();
        }

        [Get, Path("/[StorableName]/{id}")]
        public virtual ResultWithError<T> GetById(HttpContext context, int id)
        {
            ResultWithError<T> result = DM_GetById(context, id);
            if (result.Result != null)
            {
                if (result.Result.Id != id)
                {
                    Console.WriteLine("Impossible " + StorableName() + ": get " + result.Result.Id + " instead of " + id);
                }
                result.Result = OnSend(context, result.Result);
            }
            return result;
        }
        protected virtual ResultWithError<T> DM_GetById(HttpContext context, int id)
        {
            return Storable<T>.GetByIdWithError(id).ToGeneric();
        }

        [Post, Path("/[StorableName]/getbyids")]
        public virtual ResultWithError<List<T>> GetByIds(HttpContext context, List<int> ids)
        {
            ResultWithError<List<T>> result = DM_GetByIds(context, ids);
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
        protected virtual ResultWithError<List<T>> DM_GetByIds(HttpContext context, List<int> ids)
        {
            return Storable<T>.GetByIdsWithError(ids).ToGeneric();
        }


        [Put]
        [Path("/[StorableName]/{id}")]
        public virtual ResultWithError<T> Update(HttpContext context, int id, T item)
        {
            item.Id = id;
            item = OnReceive(context, item);
            ResultWithError<T> result = DM_Update(context, item);
            if (result.Result != null)
            {
                result.Result = OnSend(context, item);
            }
            return result;
        }
        protected virtual ResultWithError<T> DM_Update(HttpContext context, T item)
        {
            return Storable<T>.UpdateWithError(item).ToGeneric();
        }

        [Put]
        [Path("/[StorableName]s")]
        public virtual ResultWithError<List<T>> UpdateMany(HttpContext context, List<T> list)
        {
            List<T> _list = new();
            foreach (T item in list)
            {
                _list.Add(OnReceive(context, item));
            }
            ResultWithError<List<T>> result = DM_UpdateMany(context, _list);
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

        protected virtual ResultWithError<List<T>> DM_UpdateMany(HttpContext context, List<T> list)
        {
            return Storable<T>.UpdateWithError(list).ToGeneric();
        }

        [Delete, Path("/[StorableName]/{id}")]
        public virtual ResultWithError<T> Delete(HttpContext context, int id)
        {
            ResultWithError<T> result = DM_Delete(context, id);
            if (result.Result != null)
            {
                result.Result = OnSend(context, result.Result);
            }
            return result;
        }
        protected virtual ResultWithError<T> DM_Delete(HttpContext context, int id)
        {
            return Storable<T>.DeleteWithError(id).ToGeneric();
        }

        [Delete, Path("/[StorableName]s")]
        public virtual ResultWithError<List<T>> DeleteMany(HttpContext context, List<int> ids)
        {
            ResultWithError<List<T>> result = DM_DeleteMany(context, ids);
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

        protected virtual ResultWithError<List<T>> DM_DeleteMany(HttpContext context, List<int> ids)
        {
            return Storable<T>.DeleteWithError(ids).ToGeneric();
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
