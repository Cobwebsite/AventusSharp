using System;
using System.Collections.Generic;

namespace AventusSharp.WebSocket.Attributes
{
    public enum ResponseTypeEnum
    {
        Single,
        Others,
        Broadcast,
        Custom
    }


    [AttributeUsage(AttributeTargets.Method)]
    public class ResponseType : Attribute
    {
        public ResponseTypeEnum Type { get; private set; }

        public Func<WsEndPoint, WebSocketConnection?, List<WebSocketConnection>>? CustomFct { get; protected set; }

        public ResponseType(ResponseTypeEnum type, Func<WsEndPoint, WebSocketConnection?, List<WebSocketConnection>>? customFct)
        {
            Type = type;
            CustomFct = customFct;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class Broadcast : ResponseType
    {
        public Broadcast() : base(ResponseTypeEnum.Broadcast, null)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class Others : ResponseType
    {
        public Others() : base(ResponseTypeEnum.Others, null)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public abstract class Custom : ResponseType
    {
        public Custom() : base(ResponseTypeEnum.Custom, null)
        {
            CustomFct = GetConnections;
        }

        public abstract List<WebSocketConnection> GetConnections(WsEndPoint endPoint, WebSocketConnection? connection);
    }
}
