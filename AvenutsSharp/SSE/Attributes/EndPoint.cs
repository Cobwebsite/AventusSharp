using System;

namespace AventusSharp.SSE.Attributes
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
    public class EndPoint<T> : EndPoint where T : ISSEEndPoint, new()
    {
        
        public EndPoint() : base(typeof(T))
        {
        }
    }
}
