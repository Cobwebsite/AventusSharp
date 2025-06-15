using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using AventusSharp.WebSocket.Request;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AventusSharp.WebSocket
{
    /// <summary>
    /// Class that represent a connection Ws between Client and Server
    /// </summary>
    public class WebSocketConnection
    {
        public static bool DisplayMsg { get; set; }
        public string SessionId { get; private set; }
        private readonly HttpContext context;
        private readonly System.Net.WebSockets.WebSocket webSocket;
        public readonly WsEndPoint instance;
        private WebSocketReceiveResult? result;

        private bool IsStopped = false;

        private CancellationTokenSource tokenSource;

        /// <summary>
        /// get context of the request
        /// </summary>
        /// <returns></returns>
        public HttpContext GetContext()
        {
            return context;
        }
        /// <summary>
        /// get WebSocket class from c#
        /// </summary>
        /// <returns></returns>
        public System.Net.WebSockets.WebSocket GetWebSocket()
        {
            return webSocket;
        }
        /// <summary>
        /// default constructor
        /// </summary>
        /// <param name="context">context of request HTTP</param>
        /// <param name="webSocket">websocket create</param>
        /// <param name="instance">Instance of WebsocketInstance (parent)</param>
        public WebSocketConnection(HttpContext context, System.Net.WebSockets.WebSocket webSocket, WsEndPoint instance)
        {
            this.context = context;
            SessionId = context.Session.Id;
            this.webSocket = webSocket;
            this.instance = instance;
            tokenSource = new CancellationTokenSource();
        }
        /// <summary>
        /// Start the WebSocket connection
        /// </summary>
        /// <returns></returns>
        public async Task Start()
        {
            byte[] buffer = new byte[1024 * 4];
            bool websocketHasError = false;
            try
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), tokenSource.Token);
                string msg = "";

                while (!result.CloseStatus.HasValue && !IsStopped)
                {
                    websocketHasError = false;
                    byte[] buffTemp = new byte[result.Count];
                    for (int i = 0; i < buffTemp.Length; i++)
                    {
                        buffTemp[i] = buffer[i];
                    }
                    msg += Encoding.UTF8.GetString(buffTemp);
                    if (result.EndOfMessage)
                    {
                        try
                        {

                            JsonNode _o = JsonValue.Create(msg);
                            if (_o is JsonObject o)
                            {
                                if (o["channel"]?.ToString() == "ping")
                                {
                                    _ = Send("pong", new JsonObject());
                                }
                                else
                                {
                                    if (DisplayMsg)
                                    {
                                        Console.WriteLine("on a reçu sur " + o["channel"], "onMessage");
                                    }
                                    string? channel = o["channel"]?.ToString().ToLower();
                                    if (channel == null)
                                    {
                                        return;
                                    }

                                    string? bodyTxt = o.TryGetPropertyValue("data", out var node) ? node?.ToString() : null;
                                    WebSocketRouterBody body = new(bodyTxt);

                                    string? uid = o.TryGetPropertyValue("uid", out var node2) ? node2?.ToString() : null;
                                    if (uid != null)
                                    {
                                        await instance.Route(this, channel, body, uid.ToString());
                                    }
                                    else
                                    {
                                        await instance.Route(this, channel, body);
                                    }
                                }

                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error on parse message from socket : " + e.Message, "errorParsingMessage");
                        }
                        msg = "";
                    }
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), tokenSource.Token);
                }
                WebSocketCloseStatus status = result.CloseStatus != null ? result.CloseStatus.Value : WebSocketCloseStatus.Empty;
                await webSocket.CloseAsync(status, result.CloseStatusDescription, CancellationToken.None);
            }
            catch (Exception e)
            {
                if (!websocketHasError && !(e is OperationCanceledException))
                {
                    websocketHasError = true;
                }
            }
            instance.RemoveInstance(this);
        }

        public async Task Close()
        {
            try
            {
                IsStopped = true;
                tokenSource.Cancel();
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
        }

        #region Send
        /// <summary>
        /// Send a msg though this connection
        /// </summary>
        /// <param name="eventName">Event name</param>
        /// <param name="data">string to send</param>
        /// <param name="uid">Uid to identify request</param>
        /// <returns></returns>
        private async Task Send(string eventName, string data, string? uid = null)
        {
            JsonObject toSend = new()
            {
                ["channel"] = eventName,
                ["data"] = data,
            };
            if (!string.IsNullOrEmpty(uid))
            {
                toSend["uid"] = uid;
            }
            byte[] dataToSend = Encoding.UTF8.GetBytes(toSend.ToJsonString());

            await Send(dataToSend);
        }
        /// <summary>
        /// Send a msg though this connection
        /// </summary>
        /// <param name="eventName">Event name</param>
        /// <param name="o">Object to send</param>
        /// <param name="uid">Uid to identify request</param>
        /// <returns></returns>
        private async Task Send(string eventName, JsonObject o, string? uid = null)
        {
            string data = o.ToJsonString();
            await Send(eventName, data, uid);
        }

        /// <summary>
        /// Send a msg though this connection
        /// </summary>
        /// <param name="dataToSend">Object to send</param>
        /// <returns></returns>
        internal async Task Send(byte[] dataToSend)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                {
                    await webSocket.SendAsync(dataToSend, WebSocketMessageType.Text, true, tokenSource.Token);
                }
                else
                {
                    this.instance.RemoveInstance(this);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in RouterSocket.send() : " + e.ToString());
                this.instance.RemoveInstance(this);
            }
        }
        /// <summary>
        /// Send a msg though this connection
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="obj"></param>
        /// <param name="uid"></param>
        /// <returns></returns>
        public async Task Send(string eventName, object? obj = null, string? uid = null)
        {
            try
            {
                if (obj != null)
                {
                    string json = JsonSerializer.Serialize(obj, instance.settings);
                    await Send(eventName, json, uid);
                }
                else
                {
                    await Send(eventName, new JsonObject(), uid);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        #endregion
    }


}
