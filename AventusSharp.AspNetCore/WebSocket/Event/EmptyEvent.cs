using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket.Attributes;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AventusSharp.WebSocket.Event
{
    [NoExport]
    public class EmptyEvent : WebSocketEvent
    {
        internal override async Task _Emit()
        {
            path = basePath;
            await DefaultEmit(null);
        }
    }
}
