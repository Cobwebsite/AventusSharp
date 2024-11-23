using System;

namespace AventusSharp.WebSocket.Attributes
{
    /// <summary>
    /// Determine if the typescript must start listening this event when the connection open
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ListenOnBoot : Attribute
    {
        public bool listen { get; private set; }
        public ListenOnBoot(bool listen = true)
        {
            this.listen = listen;
        }
    }
}
