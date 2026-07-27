using AventusSharp.SSE.Attributes;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace AventusSharp.SSE.Event
{
    public interface ISSEEvent
    {

    }
    [NoExport]
    public abstract class GenericSSEEvent : ISSEEvent
    {
        protected SSEEndPoint? endPoint { get; set; }
        protected SSEConnection? connection { get; set; }
        protected Func<SSEEndPoint, SSEConnection?, List<SSEConnection>>? CustomFct;


        public GenericSSEEvent()
        {

        }

        protected abstract string GetTopic();
        protected abstract object? GetContent();

        protected List<SSEConnection> FilterConnection(SSEEndPoint endPoint)
        {
            return endPoint.GetConnectionsSnapshot();
        }


        public Task EmitTo(SSEConnection connection)
        {
            this.connection = connection;
            return DefaultEmit();
        }
        public Task EmitTo(SSEEndPoint endPoint)
        {
            this.endPoint = endPoint;
            return DefaultEmit();
        }

        public async Task<VoidWithError> EmitTo<U>() where U : SSEEndPoint
        {
            VoidWithError result = PrepareEndPointType(typeof(U));
            if (!result.Success) return result;
            await DefaultEmit();
            return result;
        }

        public async Task<VoidWithError> Emit()
        {
            VoidWithError result = new VoidWithError();
            List<Attribute> attributes = GetType().GetCustomAttributes().ToList();
            foreach (Attribute attribute in attributes)
            {
                if (attribute is EndPoint endPointAttr)
                {
                    result.Run(() => PrepareEndPointType(endPointAttr.endpoint));
                }
            }
            if (!result.Success) return result;
            await DefaultEmit();
            return result;
        }

        protected VoidWithError PrepareEndPointType(Type endPointType)
        {
            VoidWithError result = new VoidWithError();
            SSEEndPoint? endPoint = SSEMiddleware.endPointInstances.Values.FirstOrDefault(p => p.GetType() == endPointType);
            if (endPoint == null)
            {
                result.Errors.Add(new SSEError(SSEErrorCode.NoEndPoint, "No endpoint of type " + endPointType.Name + " found. Did you register the SSEMiddleware?"));
            }
            else
            {
                this.endPoint = endPoint;
            }
            return result;
        }


        protected async Task DefaultEmit()
        {
            object? o = GetContent();
            if (connection != null)
            {
                await connection.Send(GetTopic(), o);
            }
            else if (endPoint != null)
            {
                await endPoint.Broadcast(GetTopic(), o, FilterConnection(endPoint));
            }
            else
            {
                SSEEndPoint main = SSEMiddleware.GetMain();
                await main.Broadcast(GetTopic(), o, FilterConnection(main));
            }
        }
    }

    public abstract class SSEEvent<T> : GenericSSEEvent
    {
        protected abstract T GetBody();
        protected override object? GetContent()
        {
            return GetBody();
        }

    }
    public abstract class SSEEmptyEvent : GenericSSEEvent
    {
        protected override sealed object? GetContent()
        {
            return null;
        }
    }
}
