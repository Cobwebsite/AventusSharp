using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace AventusSharp.WebSocket.Event
{
    public interface IWebSocketEvent
    {

    }
    [NoExport]
    public abstract class WebSocketEvent : IWebSocketEvent
    {
        protected WsEndPoint? endPoint { get; set; }
        protected WebSocketConnection? connection { get; set; }
        protected string uid { get; private set; } = "";
        protected string basePath { get; set; } = "";
        protected string? path { get; set; } = null;
        protected ResponseTypeEnum? eventType { get; set; }

        protected Func<WsEndPoint, WebSocketConnection?, List<WebSocketConnection>>? CustomFct;


        public WebSocketEvent()
        {

        }

        public void Configure(string basePath, WsEndPoint endPoint, WebSocketConnection connection, string uid, ResponseTypeEnum eventType)
        {
            this.eventType = eventType;
            this.endPoint = endPoint;
            this.connection = connection;
            this.uid = uid;
            if (this.basePath == "")
            {
                this.basePath = basePath;
            }
        }

        public void Configure(string basePath, WsEndPoint endPoint, WebSocketConnection connection, string uid, ResponseTypeEnum eventType, Func<WsEndPoint, WebSocketConnection?, List<WebSocketConnection>>? customFct)
        {
            Configure(basePath, endPoint, connection, uid, eventType);
            CustomFct = customFct;
        }

        public Task EmitTo(WebSocketConnection connection, string uid = "")
        {
            this.uid = uid;
            this.eventType = ResponseTypeEnum.Single;
            this.connection = connection;
            return _Emit();
        }
        public Task EmitTo(WsEndPoint endPoint, string uid = "")
        {
            this.uid = uid;
            eventType = ResponseTypeEnum.Broadcast;
            this.endPoint = endPoint;
            return _Emit();
        }

        public async Task<VoidWithError> EmitTo<T>(string uid = "") where T : WsEndPoint
        {
            VoidWithError result = PrepareEndPointType(typeof(T));
            if (!result.Success) return result;
            this.uid = uid;
            eventType = ResponseTypeEnum.Broadcast;
            await _Emit();
            return result;
        }

        public async Task<VoidWithError> Emit(string uid = "")
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
            this.uid = uid;
            eventType = ResponseTypeEnum.Broadcast;
            await _Emit();
            return result;
        }

        protected VoidWithError PrepareEndPointType(Type endPointType)
        {
            VoidWithError result = new VoidWithError();
            WsEndPoint? endPoint = WebSocketMiddleware.endPointInstances.Values.FirstOrDefault(p => p.GetType() == endPointType);
            if (endPoint == null)
            {
                result.Errors.Add(new WsError(WsErrorCode.NoEndPoint, "No endpoint of type " + endPointType.Name + " found. Did you register the WebSocketMiddleware?"));
            }
            else
            {
                this.endPoint = endPoint;
            }
            return result;
        }


        internal abstract Task _Emit();

        protected async Task DefaultEmit(object? o)
        {
            if (path == null)
            {
                throw new WsError(WsErrorCode.NoPath, "The path isn't transformed from basePath " + basePath).GetException();
            }
            if (eventType == ResponseTypeEnum.Single)
            {
                if (connection == null)
                {
                    throw new WsError(WsErrorCode.NoConnection, "You must provide a connection").GetException();
                }
                await connection.Send(path, o, uid);
            }
            else if (endPoint != null)
            {
                if (eventType == ResponseTypeEnum.Custom && CustomFct != null)
                {
                    await endPoint.Broadcast(path, o, uid, CustomFct(endPoint, connection));
                }
                else if (eventType == ResponseTypeEnum.Others && connection != null)
                {
                    await endPoint.Broadcast(path, o, uid, null, new List<WebSocketConnection>() { connection });
                }
                else
                {
                    await endPoint.Broadcast(path, o, uid);
                }
            }
            else
            {
                throw new WsError(WsErrorCode.NoEndPoint, "You must provide a endpoint. Maybe you can use the function EmitTo()").GetException();
            }
        }
    }
}
