using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AventusSharp.AspNetCore.Hosting
{
    internal static class ConcurrentBroadcast
    {
        internal const int MaxConcurrentConnections = 16;

        internal static async Task Send<T>(IEnumerable<T> connections, Func<T, Task> send, Action<Exception> onError)
        {
            var pending = new List<Task>(MaxConcurrentConnections);
            foreach (T connection in connections)
            {
                if (pending.Count == MaxConcurrentConnections)
                {
                    Task completed = await Task.WhenAny(pending);
                    pending.Remove(completed);
                    try { await completed; } catch (Exception error) { onError(error); }
                }

                try { pending.Add(send(connection)); } catch (Exception error) { onError(error); }
            }

            foreach (Task task in pending)
            {
                try { await task; } catch (Exception error) { onError(error); }
            }
        }
    }
}
