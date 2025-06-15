using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AventusSharp.WebSocket.Request
{
    public class WebSocketRouterBody
    {
        private JsonObject data = new JsonObject();
        public WebSocketRouterBody(string? content)
        {
            try
            {
                if (!string.IsNullOrEmpty(content))
                {
                    JsonNode? node = JsonNode.Parse(content);
                    if (node is JsonObject obj)
                        data = obj;
                }
            }
            catch { }
        }


        /// <summary>
        /// Transform data into object T. Add path to tell where to find data to cast
        /// </summary>
        /// <param name="type">Type needed</param>
        /// <param name="propPath">Path where to find data</param>
        /// <returns></returns>
        public ResultWithWsError<object> GetData(Type type, string propPath)
        {
            ResultWithWsError<object> result = new();

            try
            {
                JsonNode? dataToUse = data;
                string[] props = propPath.Split(".");
                foreach (string prop in props)
                {
                    if (!string.IsNullOrEmpty(prop))
                    {
                        if (dataToUse is JsonObject obj && obj.TryGetPropertyValue(prop, out var next))
                        {
                            dataToUse = next;
                        }
                        else if (dataToUse is JsonArray arr && int.TryParse(prop, out int index) && index < arr.Count)
                        {
                            dataToUse = arr[index];
                        }
                        else
                        {
                            result.Errors.Add(new WsError(WsErrorCode.CantGetValueFromBody, "Can't find path " + propPath + " in your websocket body"));
                            return result;
                        }
                    }
                }

                if (dataToUse != null)
                {
                    var jsonString = dataToUse.ToJsonString();
                    object? temp = JsonSerializer.Deserialize(jsonString, type, WebSocketMiddleware.config.JSONSettings);
                    if (temp != null)
                    {
                        result.Result = temp;
                    }
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new WsError(WsErrorCode.UnknowError, e));
            }
            return result;
        }

    }
}
