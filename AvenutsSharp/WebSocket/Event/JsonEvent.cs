using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket.Attributes;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AventusSharp.WebSocket.Event
{
    [NoExport]
    public class JsonEvent : WebSocketEvent
    {
        private object? o;

        public JsonEvent(object? o)
        {
            this.o = o;
        }

        internal override async Task _Emit()
        {
            path = basePath;
            await DefaultEmit(o);
        }
    }
}
