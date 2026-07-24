using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace AventusSharp.WebSocket.Event
{
    internal class WsEventConfig
    {
        public static Dictionary<Type, WsEventConfig> configs = new Dictionary<Type, WsEventConfig>();

        public WsEndPoint endPoint;
        public string path;
        public string basePath;
        public ResponseTypeEnum eventType;

        public WsEventConfig(WsEndPoint endPoint, string basePath, string path, ResponseTypeEnum eventType)
        {
            this.endPoint = endPoint;
            this.path = path;
            this.basePath = basePath;
            this.eventType = eventType;
        }
    }

    [NoExport]
    public abstract class WsEvent<T> : WebSocketEvent
    {
        public WsEvent()
        {
            WsEventConfig config = GetConfig();
            this.basePath = config.path;
            this.endPoint = config.endPoint;
            this.eventType = config.eventType;

        }

        private WsEventConfig GetConfig()
        {
            Type type = this.GetType();
            if (!WsEventConfig.configs.ContainsKey(type))
            {
                WsEndPoint endPoint;
                string path;
                string basePath;
                WebSocketAttributeAnalyze infoMethod = WebSocketMiddleware.PrepareAttributes(GetType().GetCustomAttributes(), "");
                if (infoMethod.endPoints.Count == 0)
                {
                    endPoint = WebSocketMiddleware.GetMain();
                }
                else
                {
                    endPoint = infoMethod.endPoints[0];
                }

                if (infoMethod.pathes.Count == 0)
                {
                    path = this.GetType().FullName ?? "";//prepare base on full classname;
                }
                else
                {
                    path = infoMethod.pathes[0];
                }
                basePath = path;
                if (!path.Contains("[") && !path.Contains("]"))
                {
                    // nothing to transform
                    path = basePath;
                }
                WsEventConfig.configs[type] = new WsEventConfig(endPoint, basePath, path, infoMethod.eventType);
            }

            return WsEventConfig.configs[type];
        }


        internal override async Task _Emit()
        {
            T o = await Prepare();
            if (path == null)
            {
                Func<string, Dictionary<string, WebSocketRouterParameterInfo>, object, bool, string> transformPattern = WebSocketMiddleware.config.transformPattern ?? WebSocketMiddleware.PrepareUrl;
                path = transformPattern(basePath, new(), this, true);
            }
            await DefaultEmit(o);
        }

        protected abstract Task<T> Prepare();
    }
}
