using AventusSharp.WebSocket.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AventusSharp.WebSocket
{
    public class WebSocketRouteInfo
    {
        public Regex pattern;
        public MethodInfo action;
        public IWsRouter router;
        public int nbParamsFunction;
        public Dictionary<string, WebSocketRouterParameterInfo> parameters = new Dictionary<string, WebSocketRouterParameterInfo>();
        public ResponseTypeEnum eventType;
        public Func<WsEndPoint, WebSocketConnection?, List<WebSocketConnection>>? CustomFct;
        public WsEndPoint? endpoint;
        public string UniqueKey
        {
            get => pattern.ToString();
        }

        public WebSocketRouteInfo(Regex pattern, MethodInfo action, IWsRouter router, int nbParamsFunction, ResponseTypeEnum eventType, Func<WsEndPoint, WebSocketConnection?, List<WebSocketConnection>>? customFct)
        {
            this.pattern = pattern;
            this.action = action;
            this.router = router;
            this.nbParamsFunction = nbParamsFunction;
            this.eventType = eventType;
            CustomFct = customFct;
        }

        public override string ToString()
        {
            string txt = "";
            if(endpoint != null)
            {
                txt += endpoint.Path + " => ";
            }
            txt += pattern.ToString();
            return txt;
        }
    }

    public class WebSocketRouterParameterInfo
    {
        public string name;
        public Type type;
        public int positionCSharp = -1;
        public int positionUrl = -1;
        public bool optional = false;

        public WebSocketRouterParameterInfo(string name, Type type)
        {
            this.name = name;
            this.type = type;
        }
    }
}
