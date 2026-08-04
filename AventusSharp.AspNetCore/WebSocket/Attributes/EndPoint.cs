using System;

namespace AventusSharp.WebSocket.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class EndPoint : Attribute
    {
        public Type endpoint { get; private set; }

        public EndPoint(Type type)
        {
            endpoint = type;
        }

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class EndPoint<T> : EndPoint where T : IWsEndPoint, new()
    {
        
        public EndPoint() : base(typeof(T))
        {
        }
    }
}
