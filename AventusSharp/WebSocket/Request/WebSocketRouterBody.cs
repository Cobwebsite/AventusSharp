using AventusSharp.Routes;
using HttpMultipartParser;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;

namespace AventusSharp.WebSocket.Request
{
    public class WebSocketRouterBody
    {
        private JObject data = new JObject();
        public WebSocketRouterBody(string? content)
        {
            try
            {
                if (content != null)
                    data = JObject.Parse(content);
            }
            catch { }
        }


        /// <summary>
        /// Transform data into object T. Add path to tell where to find data to cast
        /// </summary>
        /// <param name="type">Type needed</param>
        /// <param name="propPath">Path where to find data</param>
        /// <param name="isOptional">Determine if data is required</param>
        /// <returns></returns>
        public ResultWithWsError<object> GetData(Type type, string propPath, bool isOptional)
        {
            ResultWithWsError<object> result = new();

            try
            {
                JToken? dataToUse = data;
                string[] props = propPath.Split(".");
                foreach (string prop in props)
                {
                    if (dataToUse == null)
                    {
                        if (!isOptional)
                        {
                            result.Errors.Add(new WsError(WsErrorCode.CantGetValueFromBody, "Can't find path " + propPath + " in your websocket body"));
                            return result;
                        }
                        break;
                    }
                    if (!string.IsNullOrEmpty(prop))
                    {
                        dataToUse = dataToUse[prop];
                        if (dataToUse == null && !isOptional)
                        {
                            result.Errors.Add(new WsError(WsErrorCode.CantGetValueFromBody, "Can't find path " + propPath + " in your websocket body"));
                            return result;
                        }
                    }
                }
                string txt = JsonConvert.SerializeObject(dataToUse);
                object? temp = JsonConvert.DeserializeObject(txt, type, WebSocketMiddleware.config.JSONSettings);
                if (temp != null)
                {
                    result.Result = temp;
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new WsError(WsErrorCode.UnknowError, e));
            }
            return result;
        }


        public override string ToString()
        {
            return data.ToString();
        }
    }
}
