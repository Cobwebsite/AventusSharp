using AventusSharp.Data;
using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket.Attributes;
using Path = AventusSharp.WebSocket.Attributes.Path;
using AventusSharp.WebSocket.Event;
using AventusSharp.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AventusSharp.WebSocket
{
    public interface IStorableWsRouter
    {

    }
    public abstract class StorableWsRouter<T, U, R, S> : WsRouter, IStorableWsRouter
        where T : IStorable
        where U : Routes.Request.Request
        where R : Routes.Response.Resource
        where S : Routes.Response.Resource
    {

        protected IGenericDM<T>? DM;
        protected bool PreventEmitCreated { get; set; } = false;
        protected bool PreventEmitUpdated { get; set; } = false;
        protected bool PreventEmitDeleted { get; set; } = false;
        public StorableWsRouter()
        {
            this.DM = GetDM();
            BindEvents();
        }

        protected void BindEvents()
        {
            if (DM != null)
            {
                DM.OnCreated += DM_OnCreated;
                DM.OnUpdated += DM_OnUpdated;
                DM.OnDeleted += DM_OnDeleted;
            }
        }

        private void DM_OnCreated(ResultWithError<List<T>> result)
        {
            if (!PreventEmitCreated)
            {
                StorableWsRoute_CreateMany<T> @event = new StorableWsRoute_CreateMany<T>(result, StorableName);
                TriggerEvent(@event);
            }
        }
        private void DM_OnUpdated(ResultWithError<List<T>> result)
        {
            if (!PreventEmitUpdated)
            {
                StorableWsRoute_UpdateMany<T> @event = new StorableWsRoute_UpdateMany<T>(result, StorableName);
                TriggerEvent(@event);
            }
        }
        private void DM_OnDeleted(ResultWithError<List<T>> result)
        {
            if (!PreventEmitDeleted)
            {
                StorableWsRoute_DeleteMany @event = new StorableWsRoute_DeleteMany<T>(result, StorableName);
                TriggerEvent(@event);
            }
        }

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


        protected abstract IGenericDM<T>? GetDM();

        [Path("/[StorableName]")]
        public virtual async Task<ResultWithError<List<R>>> GetAll(IAventusContext context)
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
        protected virtual async Task<ResultWithError<List<T>>> DM_GetAll(IAventusContext context)
        {
            return (await Storable<T>.GetAllWithError()).ToGeneric();
        }

        [Path("/[StorableName]/Create"), Broadcast]
        public virtual async Task<ResultWithError<S>> Create(IAventusContext context, U request)
        {
            T item = ConvertRequest(context, request);
            PreventEmitCreated = true;
            ResultWithError<S> result = new ResultWithError<S>();
            T? temp = await result.ExtractAsync(() => DM_Create(context, item));
            if (temp != null)
            {
                result.Result = ConvertResourceDetails(context, temp);
            }
            PreventEmitCreated = false;
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Create(IAventusContext context, T item)
        {
            return (await Storable<T>.CreateWithError(item)).ToGeneric();
        }

        [Path("/[StorableName]/CreateMany"), Broadcast]
        public virtual async Task<StorableWsRoute_CreateMany<S>> CreateMany(IAventusContext context, List<U> list)
        {
            PreventEmitCreated = true;
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
            PreventEmitCreated = false;
            return new StorableWsRoute_CreateMany<S>(result, StorableName);
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_CreateMany(IAventusContext context, List<T> list)
        {
            return (await Storable<T>.CreateWithError(list)).ToGeneric();
        }

        [Path("/[StorableName]/{id}")]
        public virtual async Task<ResultWithError<S>> GetById(IAventusContext context, int id)
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
        protected virtual async Task<ResultWithError<T>> DM_GetById(IAventusContext context, int id)
        {
            return (await Storable<T>.GetByIdWithError(id)).ToGeneric();
        }

        [Path("/[StorableName]/getbyids")]
        public virtual async Task<ResultWithError<List<S>>> GetByIds(IAventusContext context, List<int> ids)
        {
            ResultWithError<List<S>> result = new();
            List<T>? listTemp = await result.ExtractAsync(() => DM_GetByIds(context, ids));
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
        protected virtual async Task<ResultWithError<List<T>>> DM_GetByIds(IAventusContext context, List<int> ids)
        {
            return (await Storable<T>.GetByIdsWithError(ids)).ToGeneric();
        }



        [Path("/[StorableName]/{id}/Update"), Broadcast]
        public virtual async Task<ResultWithError<S>> Update(IAventusContext context, int id, U request)
        {
            PreventEmitUpdated = true;
            ResultWithError<S> result = new();
            T item = ConvertRequest(context, request);
            item.Id = id;
            T? temp = await result.ExtractAsync(() => DM_Update(context, item));
            if (temp != null)
            {
                result.Result = ConvertResourceDetails(context, temp);
            }
            PreventEmitUpdated = false;
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Update(IAventusContext context, T item)
        {
            return (await Storable<T>.UpdateWithError(item)).ToGeneric();
        }

        [Path("/[StorableName]/UpdateMany"), Broadcast]
        public virtual async Task<StorableWsRoute_UpdateMany<S>> UpdateMany(IAventusContext context, List<U> list)
        {
            PreventEmitUpdated = true;
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
            PreventEmitUpdated = false;
            return new StorableWsRoute_UpdateMany<S>(result, StorableName);
        }

        protected virtual async Task<ResultWithError<List<T>>> DM_UpdateMany(IAventusContext context, List<T> list)
        {
            return (await Storable<T>.UpdateWithError(list)).ToGeneric();
        }

        [Path("/[StorableName]/{id}/Delete"), Broadcast]
        public virtual async Task<ResultWithError<int>> Delete(IAventusContext context, int id)
        {
            PreventEmitDeleted = true;
            ResultWithError<int> result = new();
            await result.RunAsync(() => DM_Delete(context, id));
            result.Result = id;
            PreventEmitDeleted = false;
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Delete(IAventusContext context, int id)
        {
            return (await Storable<T>.DeleteWithError(id)).ToGeneric();
        }

        [Path("/[StorableName]/DeleteMany"), Broadcast]
        public virtual async Task<StorableWsRoute_DeleteMany> DeleteMany(IAventusContext context, List<int> ids)
        {
            PreventEmitDeleted = true;
            ResultWithError<List<int>> result = new();
            List<T>? listTemp = await result.ExtractAsync(() => DM_DeleteMany(context, ids));
            if (listTemp != null)
            {
                result.Result = new();
                foreach (T item in listTemp)
                {
                    result.Result.Add(item.Id);
                }
            }
            PreventEmitDeleted = false;
            return new StorableWsRoute_DeleteMany(result, StorableName);
        }

        protected virtual async Task<ResultWithError<List<T>>> DM_DeleteMany(IAventusContext context, List<int> ids)
        {
            return (await Storable<T>.DeleteWithError(ids)).ToGeneric();
        }




        protected virtual T ConvertRequest(IAventusContext context, U item)
        {
            return Binder.AutoBind<T>(item);
        }
        protected virtual R ConvertResource(IAventusContext context, T item)
        {
            return Binder.AutoBind<R>(item);
        }
        protected virtual S ConvertResourceDetails(IAventusContext context, T item)
        {
            return Binder.AutoBind<S>(item);
        }
    }

    [NoExport]
    public abstract class StorableWsRouter<T, U, R> : StorableWsRouter<T, U, R, R>
       where T : IStorable
       where U : Routes.Request.Request
       where R : Routes.Response.Resource
    {
        protected override sealed R ConvertResourceDetails(IAventusContext context, T item)
        {
            return ConvertResource(context, item);
        }
    }

    [NoExport]
    public abstract class StorableWsRouter<T> : WsRouter, IStorableWsRouter where T : IStorable
    {

        protected IGenericDM<T>? DM;
        protected bool PreventEmitCreated { get; set; } = false;
        protected bool PreventEmitUpdated { get; set; } = false;
        protected bool PreventEmitDeleted { get; set; } = false;
        public StorableWsRouter()
        {
            this.DM = GetDM();
            BindEvents();
        }

        protected void BindEvents()
        {
            if (DM != null)
            {
                DM.OnCreated += DM_OnCreated;
                DM.OnUpdated += DM_OnUpdated;
                DM.OnDeleted += DM_OnDeleted;
            }
        }

        private void DM_OnCreated(ResultWithError<List<T>> result)
        {
            if (!PreventEmitCreated)
            {
                StorableWsRoute_CreateMany<T> @event = new StorableWsRoute_CreateMany<T>(result, StorableName);
                TriggerEvent(@event);
            }
        }
        private void DM_OnUpdated(ResultWithError<List<T>> result)
        {
            if (!PreventEmitUpdated)
            {
                StorableWsRoute_UpdateMany<T> @event = new StorableWsRoute_UpdateMany<T>(result, StorableName);
                TriggerEvent(@event);
            }
        }
        private void DM_OnDeleted(ResultWithError<List<T>> result)
        {
            if (!PreventEmitDeleted)
            {
                StorableWsRoute_DeleteMany<T> @event = new StorableWsRoute_DeleteMany<T>(result, StorableName);
                TriggerEvent(@event);
            }
        }

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


        protected abstract IGenericDM<T>? GetDM();

        [Path("/[StorableName]")]
        public virtual async Task<ResultWithError<List<T>>> GetAll(IAventusContext context)
        {
            ResultWithError<List<T>> result = await DM_GetAll(context);
            if (result.Result != null)
            {
                List<T> list = new();
                foreach (T item in result.Result)
                {
                    list.Add(OnSend(item));
                }
                result.Result = list;
            }
            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_GetAll(IAventusContext context)
        {
            return (await Storable<T>.GetAllWithError()).ToGeneric();
        }

        [Path("/[StorableName]/Create"), Broadcast]
        public virtual async Task<ResultWithError<T>> Create(IAventusContext context, T item)
        {
            PreventEmitCreated = true;
            item = OnReceive(item);
            ResultWithError<T> result = await DM_Create(context, item);
            if (result.Result != null)
            {
                result.Result = OnSend(item);
            }
            PreventEmitCreated = false;
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Create(IAventusContext context, T item)
        {
            return (await Storable<T>.CreateWithError(item)).ToGeneric();
        }

        [Path("/[StorableName]/CreateMany"), Broadcast]
        public virtual async Task<StorableWsRoute_CreateMany<T>> CreateMany(IAventusContext context, List<T> list)
        {
            PreventEmitCreated = true;
            List<T> _list = new();
            foreach (T item in list)
            {
                _list.Add(OnReceive(item));
            }
            ResultWithError<List<T>> result = await DM_CreateMany(context, _list);
            if (result.Result != null)
            {
                List<T> listTemp = new();
                foreach (T item in result.Result)
                {
                    listTemp.Add(OnSend(item));
                }
                result.Result = listTemp;
            }
            PreventEmitCreated = false;
            return new StorableWsRoute_CreateMany<T>(result, StorableName);
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_CreateMany(IAventusContext context, List<T> list)
        {
            return (await Storable<T>.CreateWithError(list)).ToGeneric();
        }

        [Path("/[StorableName]/{id}")]
        public virtual async Task<ResultWithError<T>> GetById(IAventusContext context, int id)
        {
            ResultWithError<T> result = await DM_GetById(context, id);
            if (result.Result != null)
            {
                result.Result = OnSend(result.Result);
            }
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_GetById(IAventusContext context, int id)
        {
            return (await Storable<T>.GetByIdWithError(id)).ToGeneric();
        }

        [Path("/[StorableName]/getbyids")]
        public virtual async Task<ResultWithError<List<T>>> GetByIds(IAventusContext context, List<int> ids)
        {
            ResultWithError<List<T>> result = await DM_GetByIds(context, ids);
            if (result.Result != null)
            {
                List<T> list = new();
                foreach (T item in result.Result)
                {
                    list.Add(OnSend(item));
                }
                result.Result = list;
            }
            return result;
        }
        protected virtual async Task<ResultWithError<List<T>>> DM_GetByIds(IAventusContext context, List<int> ids)
        {
            return (await Storable<T>.GetByIdsWithError(ids)).ToGeneric();
        }



        [Path("/[StorableName]/{id}/Update"), Broadcast]
        public virtual async Task<ResultWithError<T>> Update(IAventusContext context, int id, T item)
        {
            PreventEmitUpdated = true;
            item.Id = id;
            item = OnReceive(item);
            ResultWithError<T> result = await DM_Update(context, item);
            if (result.Result != null)
            {
                result.Result = OnSend(item);
            }
            PreventEmitUpdated = false;
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Update(IAventusContext context, T item)
        {
            return (await Storable<T>.UpdateWithError(item)).ToGeneric();
        }

        [Path("/[StorableName]/UpdateMany"), Broadcast]
        public virtual async Task<StorableWsRoute_UpdateMany<T>> UpdateMany(IAventusContext context, List<T> list)
        {
            PreventEmitUpdated = true;
            List<T> _list = new();
            foreach (T item in list)
            {
                _list.Add(OnReceive(item));
            }
            ResultWithError<List<T>> result = await DM_UpdateMany(context, _list);
            if (result.Result != null)
            {
                List<T> listTemp = new();
                foreach (T item in result.Result)
                {
                    listTemp.Add(OnSend(item));
                }
                result.Result = listTemp;
            }
            PreventEmitUpdated = false;
            return new StorableWsRoute_UpdateMany<T>(result, StorableName);
        }

        protected virtual async Task<ResultWithError<List<T>>> DM_UpdateMany(IAventusContext context, List<T> list)
        {
            return (await Storable<T>.UpdateWithError(list)).ToGeneric();
        }

        [Path("/[StorableName]/{id}/Delete"), Broadcast]
        public virtual async Task<ResultWithError<T>> Delete(IAventusContext context, int id)
        {
            PreventEmitDeleted = true;
            ResultWithError<T> result = await DM_Delete(context, id);
            if (result.Result != null)
            {
                result.Result = OnSend(result.Result);
            }
            PreventEmitDeleted = false;
            return result;
        }
        protected virtual async Task<ResultWithError<T>> DM_Delete(IAventusContext context, int id)
        {
            return (await Storable<T>.DeleteWithError(id)).ToGeneric();
        }

        [Path("/[StorableName]/DeleteMany"), Broadcast]
        public virtual async Task<StorableWsRoute_DeleteMany<T>> DeleteMany(IAventusContext context, List<int> ids)
        {
            PreventEmitDeleted = true;
            ResultWithError<List<T>> result = await DM_DeleteMany(context, ids);
            if (result.Result != null)
            {
                List<T> listTemp = new();
                foreach (T item in result.Result)
                {
                    listTemp.Add(OnSend(item));
                }
                result.Result = listTemp;
            }
            PreventEmitDeleted = false;
            return new StorableWsRoute_DeleteMany<T>(result, StorableName);
        }

        protected virtual async Task<ResultWithError<List<T>>> DM_DeleteMany(IAventusContext context, List<int> ids)
        {
            return (await Storable<T>.DeleteWithError(ids)).ToGeneric();
        }


        protected virtual T OnReceive(T item)
        {
            return item;
        }
        protected virtual T OnSend(T item)
        {
            return item;
        }
    }

    [Path("/[StorableName]/CreateMany")]
    public class StorableWsRoute_CreateMany<T> : WsEvent<ResultWithError<List<T>>>
    {
        private ResultWithError<List<T>> result;
        private Func<string> _StorableName;
        public StorableWsRoute_CreateMany(ResultWithError<List<T>> result, Func<string> storableName)
        {
            this.result = result;
            _StorableName = storableName;
        }

        protected override Task<ResultWithError<List<T>>> Prepare()
        {
            return Task.FromResult(result);
        }

        protected virtual string StorableName()
        {
            return _StorableName();
        }

    }

    [Path("/[StorableName]/UpdateMany")]
    public class StorableWsRoute_UpdateMany<T> : WsEvent<ResultWithError<List<T>>>
    {
        private ResultWithError<List<T>> result;
        private Func<string> _StorableName;
        public StorableWsRoute_UpdateMany(ResultWithError<List<T>> result, Func<string> storableName)
        {
            this.result = result;
            _StorableName = storableName;
        }

        protected override Task<ResultWithError<List<T>>> Prepare()
        {
            return Task.FromResult(result);
        }

        protected virtual string StorableName()
        {
            return _StorableName();
        }

    }

    [Path("/[StorableName]/DeleteMany")]
    public class StorableWsRoute_DeleteMany : WsEvent<ResultWithError<List<int>>>
    {
        protected ResultWithError<List<int>> result;
        protected Func<string>? _StorableName;
        public StorableWsRoute_DeleteMany()
        {
            result = new();
        }
        public StorableWsRoute_DeleteMany(ResultWithError<List<int>> result, Func<string> storableName)
        {
            this.result = result;
            _StorableName = storableName;
        }


        protected override Task<ResultWithError<List<int>>> Prepare()
        {
            return Task.FromResult(result);
        }

        protected virtual string StorableName()
        {
            return _StorableName != null ? _StorableName() : "";
        }

    }

    [NoExport]
    public class StorableWsRoute_DeleteMany<T> : StorableWsRoute_DeleteMany where T : IStorable
    {

        public StorableWsRoute_DeleteMany(ResultWithError<List<T>> result, Func<string> storableName)
        {
            _StorableName = storableName;
            if (result.Success && result.Result != null)
            {
                this.result.Result = new List<int>();
                foreach (var item in result.Result)
                {
                    this.result.Result.Add(item.Id);
                }
            }
        }
    }

}
