using AventusSharp.Tools;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Scriban.Parsing;

namespace AventusSharp.SSE
{
    /// <summary>
    /// Class that represent a connection SSE between Client and Server
    /// </summary>
    public class SSEConnection
    {
        public string SessionId { get; private set; }
        private readonly HttpContext context;
        private readonly TaskCompletionSource _tcs;
        private readonly CancellationTokenSource tokenSource;
        public readonly SSEEndPoint instance;

        public Task WaitForShutdown => _tcs.Task;
        /// <summary>
        /// get context of the request
        /// </summary>
        /// <returns></returns>
        public HttpContext GetContext()
        {
            return context;
        }

        /// <summary>
        /// default constructor
        /// </summary>
        /// <param name="context">context of request HTTP</param>
        /// <param name="instance">Instance of SSEInstance (parent)</param>
        public SSEConnection(HttpContext context, SSEEndPoint instance)
        {
            _tcs = new TaskCompletionSource();
            tokenSource = new CancellationTokenSource();
            context.RequestAborted.Register(() => _tcs.TrySetResult());
            this.context = context;
            SessionId = context.Session.Id;
            this.instance = instance;
        }

        public async Task Init()
        {
            context.Response.Headers.Append("Content-Type", "text/event-stream");
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("X-Accel-Buffering", "no");
            await context.Response.Body.FlushAsync();
        }

        public async Task Close()
        {
            try
            {
                _tcs.TrySetResult();
                tokenSource.Cancel();
            }
            catch { }
        }

        #region Send
        /// <summary>
        /// Send a msg though this connection
        /// </summary>
        /// <param name="eventName">Event name</param>
        /// <param name="data">string to send</param>
        /// <returns></returns>
        private async Task Send(string eventName, string data)
        {
            try
            {
                JObject toSend = new()
                {
                    { "channel", eventName },
                    { "data", data }
                };
                await context.Response.WriteAsync($"data: {toSend.ToString(Formatting.None)}\n\n", tokenSource.Token);
                await context.Response.Body.FlushAsync(tokenSource.Token);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in RouterSocket.send() : " + e.ToString());
                instance.RemoveInstance(this);
            }
        }
        /// <summary>
        /// Send a msg though this connection
        /// </summary>
        /// <param name="eventName">Event name</param>
        /// <param name="o">Object to send</param>
        /// <returns></returns>
        private async Task Send(string eventName, JObject o)
        {
            string data = o.ToString(Formatting.None);
            await Send(eventName, data);
        }

        /// <summary>
        /// Send a msg though this connection
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public async Task Send(string eventName, object? obj = null)
        {
            try
            {
                if (obj != null)
                {
                    string json = JsonConvert.SerializeObject(obj, instance.settings);
                    await Send(eventName, json);
                }
                else
                {
                    await Send(eventName, new JObject());
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
