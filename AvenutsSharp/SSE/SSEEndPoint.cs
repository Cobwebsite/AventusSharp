using AventusSharp.Routes;
using AventusSharp.Tools.Attributes;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AventusSharp.SSE
{
    public interface ISSEEndPoint
    {
        string Path { get; }
        string DefinePath();

        bool Main();
    }

    [NoExport]
    public abstract class SSEEndPoint : ISSEEndPoint
    {
        internal readonly List<SSEConnection> connections = new();
        private readonly List<Func<SSEConnection, string, Task<bool>>> middlewares = new();
        internal JsonSerializerSettings settings;
        public string Path { get; }

        public SSEEndPoint()
        {
            settings = settings == null ? new JsonSerializerSettings(SSEMiddleware.config.JSONSettings) : settings;
            Configure(settings);

            Path = DefinePath();
        }
        public abstract string DefinePath();

        public virtual bool Main()
        {
            return false;
        }

        protected virtual void Configure(JsonSerializerSettings settings)
        {

        }
        protected void setSettings(JsonSerializerSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Add action when a request go though this SSE instance
        /// </summary>
        /// <param name="action"></param>
        public SSEEndPoint Use(Func<SSEConnection, string, Task<bool>> action)
        {
            middlewares.Add(action);
            return this;
        }

        /// <summary>
        /// define if the connection can be open
        /// exemple if authentification needed, return false if not login
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public virtual bool CanOpenConnection(HttpContext context)
        {
            return true;
        }

        /// <summary>
        /// Start a new connection SSE between server and client
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal async Task StartNewInstance(HttpContext context)
        {
            if (CanOpenConnection(context))
            {
                SSEConnection connection = new(context, this);
                await connection.Init();
                connections.Add(connection);
                try
                {
                    await OnConnectionOpen(connection);
                    Console.WriteLine("waiting");
                    await connection.WaitForShutdown;
                    Console.WriteLine("done");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                connections.Remove(connection);
            }
            else
            {
                context.Response.StatusCode = 302;
            }
        }

        protected virtual Task OnConnectionOpen(SSEConnection connection)
        {
            return Task.CompletedTask;
        }
        /// <summary>
        /// Remove connection of SSE 
        /// </summary>
        /// <param name="connection"></param>
        public async void RemoveInstance(SSEConnection connection)
        {
            try
            {
                if (connections.Contains(connection))
                {
                    await OnConnectionClose(connection);
                    connections.Remove(connection);
                }
            }
            catch
            {

            }
        }
        protected virtual Task OnConnectionClose(SSEConnection connection)
        {
            return Task.CompletedTask;
        }

        public async Task Stop()
        {
            List<SSEConnection> conns = connections.ToList();
            foreach (SSEConnection connection in conns)
            {
                await connection.Close();
            }
        }


        /// <summary>
        /// Dispatch a message to all active connections
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="o"></param>
        /// <param name="connections"></param>
        /// <param name="omit"></param>
        /// <returns></returns>
        public async Task Broadcast(string eventName, JObject o, List<SSEConnection>? connections = null, List<SSEConnection>? omit = null)
        {
            try
            {
                string data = o.ToString(Formatting.None);
                await Broadcast(eventName, data, connections, omit);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Dispatch a message to all active connections
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="data"></param>
        /// <param name="connections"></param>
        /// <param name="omit"></param>
        /// <returns></returns>
        public async Task Broadcast(string eventName, string data, List<SSEConnection>? connections = null, List<SSEConnection>? omit = null)
        {
            try
            {
                if (omit == null)
                {
                    omit = new();
                }

                if (connections == null)
                {
                    connections = this.connections;
                }

                List<SSEConnection> connectionsCloned = connections.ToList();
                for (int i = 0; i < connectionsCloned.Count; i++)
                {
                    SSEConnection conn = connectionsCloned.ElementAt(i);
                    if (omit.Contains(conn))
                    {
                        continue;
                    }

                    // todo implement parallelism here
                    await conn.Send(eventName, data);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        /// <summary>
        /// Dispatch a message to all active connections
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="obj"></param>
        /// <param name="connections"></param>
        /// <param name="omit"></param>
        /// <returns></returns>
        public async Task Broadcast(string eventName, object? obj = null, List<SSEConnection>? connections = null, List<SSEConnection>? omit = null)
        {
            try
            {
                if (obj != null)
                {
                    string json = JsonConvert.SerializeObject(obj, settings);
                    await Broadcast(eventName, json, connections, omit);
                }
                else
                {
                    await Broadcast(eventName, new JObject(), connections, omit);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

    }

    [NoExport]
    public sealed class DefaultSSEEndPoint : SSEEndPoint
    {
        public override string DefinePath()
        {
            return "/sse";
        }
    }
}
