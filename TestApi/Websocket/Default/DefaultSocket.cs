using AventusSharp.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApi.Websocket.Default
{
    public class DefaultSocket : WsEndPoint
    {
        public override string DefinePath()
        {
            return "/ws";
        }

        public override bool Main()
        {
            return true;
        }
    }
}
