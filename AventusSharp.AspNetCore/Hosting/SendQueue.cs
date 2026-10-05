using System;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.AspNetCore.Hosting
{
    internal sealed class SendQueue
    {
        private readonly object gate = new();
        private Task previous = Task.CompletedTask;

        internal async Task Run(Func<Task> send, CancellationToken cancellationToken)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task waitFor;
            lock (gate)
            {
                waitFor = previous;
                previous = done.Task;
            }

            try
            {
                await waitFor.WaitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await send();
            }
            finally
            {
                done.TrySetResult();
            }
        }
    }
}
