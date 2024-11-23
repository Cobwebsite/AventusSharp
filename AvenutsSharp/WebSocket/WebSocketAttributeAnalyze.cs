using AventusSharp.WebSocket.Attributes;
using System;
using System.Collections.Generic;

namespace AventusSharp.WebSocket
{
    internal class WebSocketAttributeAnalyze
    {
        public List<string> pathes = new List<string>();
        public List<WsEndPoint> endPoints = new List<WsEndPoint>();
        public ResponseTypeEnum eventType = ResponseTypeEnum.Single;
        public Func<WsEndPoint, WebSocketConnection?, List<WebSocketConnection>>? CustomFct;
        public bool canUse = true;
    }
}
