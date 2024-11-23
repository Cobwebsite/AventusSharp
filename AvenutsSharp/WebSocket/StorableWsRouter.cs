using AventusSharp.Data;
using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using AventusSharp.WebSocket.Attributes;
using AventusSharp.WebSocket.Event;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AventusSharp.WebSocket
{
    public abstract class StorableWsRouter<T> : WsRouter where T : IStorable
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
        public virtual ResultWithError<List<T>> GetAll()
        {
            ResultWithError<List<T>> result = DM_GetAll();
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
        protected virtual ResultWithError<List<T>> DM_GetAll()
        {
            return Storable<T>.GetAllWithError().ToGeneric();
        }

        [Path("/[StorableName]/Create"), Broadcast]
        public virtual ResultWithError<T> Create(T item)
        {
            PreventEmitCreated = true;
            item = OnReceive(item);
            ResultWithError<T> result = DM_Create(item);
            if (result.Result != null)
            {
                result.Result = OnSend(item);
            }
            PreventEmitCreated = false;
            return result;
        }
        protected virtual ResultWithError<T> DM_Create(T item)
        {
            return Storable<T>.CreateWithError(item).ToGeneric();
        }

        [Path("/[StorableName]/CreateMany"), Broadcast]
        public virtual StorableWsRoute_CreateMany<T> CreateMany(List<T> list)
        {
            PreventEmitCreated = true;
            List<T> _list = new();
            foreach (T item in list)
            {
                _list.Add(OnReceive(item));
            }
            ResultWithError<List<T>> result = DM_CreateMany(_list);
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
        protected virtual ResultWithError<List<T>> DM_CreateMany(List<T> list)
        {
            return Storable<T>.CreateWithError(list).ToGeneric();
        }

        [Path("/[StorableName]/{id}")]
        public virtual ResultWithError<T> GetById(int id)
        {
            ResultWithError<T> result = DM_GetById(id);
            if (result.Result != null)
            {
                result.Result = OnSend(result.Result);
            }
            return result;
        }
        protected virtual ResultWithError<T> DM_GetById(int id)
        {
            return Storable<T>.GetByIdWithError(id).ToGeneric();
        }

        [Path("/[StorableName]/getbyids")]
        public virtual ResultWithError<List<T>> GetByIds(List<int> ids)
        {
            ResultWithError<List<T>> result = DM_GetByIds(ids);
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
        protected virtual ResultWithError<List<T>> DM_GetByIds(List<int> ids)
        {
            return Storable<T>.GetByIdsWithError(ids).ToGeneric();
        }



        [Path("/[StorableName]/{id}/Update"), Broadcast]
        public virtual ResultWithError<T> Update(int id, T item)
        {
            PreventEmitUpdated = true;
            item.Id = id;
            item = OnReceive(item);
            ResultWithError<T> result = DM_Update(item);
            if (result.Result != null)
            {
                result.Result = OnSend(item);
            }
            PreventEmitUpdated = false;
            return result;
        }
        protected virtual ResultWithError<T> DM_Update(T item)
        {
            return Storable<T>.UpdateWithError(item).ToGeneric();
        }

        [Path("/[StorableName]/UpdateMany"), Broadcast]
        public virtual StorableWsRoute_UpdateMany<T> UpdateMany(List<T> list)
        {
            PreventEmitUpdated = true;
            List<T> _list = new();
            foreach (T item in list)
            {
                _list.Add(OnReceive(item));
            }
            ResultWithError<List<T>> result = DM_UpdateMany(_list);
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

        protected virtual ResultWithError<List<T>> DM_UpdateMany(List<T> list)
        {
            return Storable<T>.UpdateWithError(list).ToGeneric();
        }

        [Path("/[StorableName]/{id}/Delete"), Broadcast]
        public virtual ResultWithError<T> Delete(int id)
        {
            PreventEmitDeleted = true;
            ResultWithError<T> result = DM_Delete(id);
            if (result.Result != null)
            {
                result.Result = OnSend(result.Result);
            }
            PreventEmitDeleted = false;
            return result;
        }
        protected virtual ResultWithError<T> DM_Delete(int id)
        {
            return Storable<T>.DeleteWithError(id).ToGeneric();
        }

        [Path("/[StorableName]/DeleteMany"), Broadcast]
        public virtual StorableWsRoute_DeleteMany<T> DeleteMany(List<int> ids)
        {
            PreventEmitDeleted = true;
            ResultWithError<List<T>> result = DM_DeleteMany(ids);
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

        protected virtual ResultWithError<List<T>> DM_DeleteMany(List<int> ids)
        {
            return Storable<T>.DeleteWithError(ids).ToGeneric();
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
    public class StorableWsRoute_CreateMany<T> : WsEvent<ResultWithError<List<T>>> where T : IStorable
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
    public class StorableWsRoute_UpdateMany<T> : WsEvent<ResultWithError<List<T>>> where T : IStorable
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
    public class StorableWsRoute_DeleteMany<T> : WsEvent<ResultWithError<List<T>>> where T : IStorable
    {
        private ResultWithError<List<T>> result;
        private Func<string> _StorableName;
        public StorableWsRoute_DeleteMany(ResultWithError<List<T>> result, Func<string> storableName)
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

}
