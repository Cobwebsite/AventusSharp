using AventusSharp.AspNetCore.Hosting;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AventusSharpTest.SSE;

[TestFixture]
public sealed class ConcurrentBroadcastTests
{
    [Test]
    public async Task Slow_client_does_not_delay_other_clients_and_concurrency_is_bounded()
    {
        var releaseSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new ConcurrentDictionary<int, byte>();
        var finished = new ConcurrentDictionary<int, byte>();
        var active = 0;
        var peak = 0;
        var errors = new ConcurrentQueue<Exception>();

        Task Send(int client)
        {
            return SendCore(client);
        }

        async Task SendCore(int client)
        {
            int count = Interlocked.Increment(ref active);
            InterlockedExtensions.Max(ref peak, count);
            started.TryAdd(client, 0);
            try
            {
                if (client == 0)
                    await releaseSlow.Task;
                else
                    await Task.Delay(10);
                finished.TryAdd(client, 0);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }

        var watch = Stopwatch.StartNew();
        Task broadcast = ConcurrentBroadcast.Send(Enumerable.Range(0, 40), Send, errors.Enqueue);
        await WaitUntil(() => finished.Count == 39, TimeSpan.FromSeconds(2));
        long fastClientsMilliseconds = watch.ElapsedMilliseconds;
        Assert.Multiple(() =>
        {
            Assert.That(finished.Count, Is.EqualTo(39));
            Assert.That(finished.ContainsKey(0), Is.False);
            Assert.That(peak, Is.LessThanOrEqualTo(ConcurrentBroadcast.MaxConcurrentConnections));
            Assert.That(errors, Is.Empty);
        });
        releaseSlow.SetResult();
        await broadcast;
        Assert.That(fastClientsMilliseconds, Is.LessThan(2000));
        Assert.That(finished.Count, Is.EqualTo(40));
    }

    [Test]
    public async Task Client_failure_does_not_stop_delivery_to_other_clients()
    {
        var received = new ConcurrentBag<int>();
        var errors = new ConcurrentBag<Exception>();
        await ConcurrentBroadcast.Send(Enumerable.Range(0, 20), client =>
        {
            if (client == 2) throw new InvalidOperationException("client failed");
            received.Add(client);
            return Task.CompletedTask;
        }, errors.Add);

        Assert.That(received.Count, Is.EqualTo(19));
        Assert.That(errors.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Concurrent_broadcast_is_faster_than_sequential_with_one_slow_client()
    {
        int[] clients = Enumerable.Range(0, 40).ToArray();
        static Task Send(int client) => Task.Delay(client == 0 ? 200 : 20);

        var watch = Stopwatch.StartNew();
        foreach (int client in clients)
            await Send(client);
        long sequentialMs = watch.ElapsedMilliseconds;

        watch.Restart();
        await ConcurrentBroadcast.Send(clients, Send, error => throw error);
        long concurrentMs = watch.ElapsedMilliseconds;
        TestContext.WriteLine($"40 clients, one slow: sequential {sequentialMs} ms; bounded concurrent {concurrentMs} ms");

        Assert.That(concurrentMs, Is.LessThan(sequentialMs / 2));
    }

    [Test]
    public async Task Writes_on_one_connection_stay_in_call_order()
    {
        var queue = new SendQueue();
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new List<int>();
        Task first = queue.Run(async () =>
        {
            observed.Add(1);
            await releaseFirst.Task;
        }, CancellationToken.None);
        Task second = queue.Run(() =>
        {
            observed.Add(2);
            return Task.CompletedTask;
        }, CancellationToken.None);
        Assert.That(observed, Is.EqualTo(new[] { 1 }));
        releaseFirst.SetResult();
        await Task.WhenAll(first, second);
        Assert.That(observed, Is.EqualTo(new[] { 1, 2 }));
    }

    private static class InterlockedExtensions
    {
        internal static void Max(ref int location, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref location);
                if (current >= value) return;
            } while (Interlocked.CompareExchange(ref location, value, current) != current);
        }
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!condition())
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await Task.Delay(5, cancellation.Token);
        }
    }
}
