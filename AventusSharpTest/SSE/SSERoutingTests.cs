using AventusSharp.SSE;
using AventusSharp.SSE.Event;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace AventusSharpTest.SSE;

[TestFixture]
[NonParallelizable]
public sealed class SSERoutingTests
{
    [SetUp]
    public void ResetEndpoint()
    {
        TestSseEndPoint.Reset(allow: true);
        TestSseEndPoint.Instance!.connections.Clear();
    }

    [OneTimeSetUp]
    public void RegisterEndpoint()
    {
        var result = SSEMiddleware.Register(new[] { typeof(TestSseEndPoint) });
        Assert.That(result.Success, Is.True,
            string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }

    [Test]
    public async Task Unknown_path_calls_next_middleware()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/unknown-sse";
        var called = false;

        await SSEMiddleware.OnRequest(context, () =>
        {
            called = true;
            return Task.CompletedTask;
        });

        Assert.That(called, Is.True);
    }

    [Test]
    public async Task Registered_endpoint_initializes_event_stream_response()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext();
        context.Request.Path = "/tests-sse";
        context.RequestAborted = cancellation.Token;
        context.Response.Body = new MemoryStream();

        await SSEMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.ContentType,
                Does.StartWith("text/event-stream"));
            Assert.That(context.Response.Headers.CacheControl.ToString(),
                Is.EqualTo("no-cache"));
            Assert.That(context.Response.Headers["X-Accel-Buffering"].ToString(),
                Is.EqualTo("no"));
            Assert.That(ReadBody(context), Is.EqualTo(": connected\n\n"));
        });
    }

    [Test]
    public async Task Request_abort_calls_open_and_close_callbacks_and_clears_scope()
    {
        TestSseEndPoint.Reset(allow: true);
        using var cancellation = new CancellationTokenSource();
        var context = CreateContext(cancellation.Token);
        var request = SSEMiddleware.OnRequest(
            context,
            () => Task.CompletedTask);
        await TestSseEndPoint.Opened.Task;

        cancellation.Cancel();
        await request;

        Assert.Multiple(() =>
        {
            Assert.That(TestSseEndPoint.OpenCount, Is.EqualTo(1));
            Assert.That(TestSseEndPoint.CloseCount, Is.EqualTo(1));
            Assert.That(TestSseEndPoint.LastConnection, Is.Not.Null);
            Assert.That(TestSseEndPoint.LastConnection!.WaitForShutdown.IsCompleted,
                Is.True);
            Assert.That(AventusSharp.Routes.RouterMiddleware.AventusContextScope,
                Is.Null);
        });
    }

    [Test]
    public void Open_callback_failure_still_closes_and_removes_the_connection()
    {
        TestSseEndPoint.ThrowOnOpen = true;
        var context = CreateContext(CancellationToken.None);

        Assert.DoesNotThrowAsync(async () =>
            await SSEMiddleware.OnRequest(context, () => Task.CompletedTask));

        Assert.Multiple(() =>
        {
            Assert.That(TestSseEndPoint.OpenCount, Is.EqualTo(1));
            Assert.That(TestSseEndPoint.CloseCount, Is.EqualTo(1));
            Assert.That(Connections(), Is.Empty);
            Assert.That(AventusSharp.Routes.RouterMiddleware.AventusContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Close_callback_failure_does_not_escape_or_leave_the_connection_tracked()
    {
        TestSseEndPoint.ThrowOnClose = true;
        using var cancellation = new CancellationTokenSource();
        var context = CreateContext(cancellation.Token);
        var request = SSEMiddleware.OnRequest(
            context,
            () => Task.CompletedTask);
        await TestSseEndPoint.Opened.Task;

        cancellation.Cancel();

        Assert.DoesNotThrowAsync(async () => await request);
        Assert.Multiple(() =>
        {
            Assert.That(TestSseEndPoint.CloseCount, Is.EqualTo(1));
            Assert.That(Connections(), Is.Empty);
            Assert.That(AventusSharp.Routes.RouterMiddleware.AventusContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Endpoint_can_refuse_connection_without_calling_next()
    {
        TestSseEndPoint.Reset(allow: false);
        var context = CreateContext(CancellationToken.None);
        var nextCalled = false;

        await SSEMiddleware.OnRequest(context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(302));
            Assert.That(nextCalled, Is.False);
            Assert.That(TestSseEndPoint.OpenCount, Is.Zero);
            Assert.That(TestSseEndPoint.CloseCount, Is.Zero);
        });
    }

    [Test]
    public async Task Broadcast_sends_formatted_event_to_selected_connections_and_omits_requested_ones()
    {
        var endpoint = TestSseEndPoint.Instance!;
        var firstContext = CreateContext(CancellationToken.None);
        var secondContext = CreateContext(CancellationToken.None);
        var first = new SSEConnection(firstContext, endpoint);
        var second = new SSEConnection(secondContext, endpoint);

        await endpoint.Broadcast(
            "status",
            new { Value = 7 },
            [first, second],
            [second]);

        var envelope = ReadSingleEvent(firstContext);
        var data = JObject.Parse(envelope["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(envelope["channel"]?.Value<string>(),
                Is.EqualTo("status"));
            Assert.That(data["Value"]?.Value<int>(), Is.EqualTo(7));
            Assert.That(ReadBody(secondContext), Is.Empty);
        });
    }

    [Test]
    public async Task Sse_event_can_emit_to_a_single_connection()
    {
        var endpoint = TestSseEndPoint.Instance!;
        var context = CreateContext(CancellationToken.None);
        var connection = new SSEConnection(context, endpoint);

        await new StatusEvent(12).EmitTo(connection);

        var envelope = ReadSingleEvent(context);
        var data = JObject.Parse(envelope["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(envelope["channel"]?.Value<string>(),
                Is.EqualTo("status-event"));
            Assert.That(data["Value"]?.Value<int>(), Is.EqualTo(12));
        });
    }

    [Test]
    public async Task Write_failure_removes_connection_and_calls_close_callback()
    {
        var endpoint = TestSseEndPoint.Instance!;
        var context = CreateContext(CancellationToken.None);
        context.Response.Body = new ThrowingWriteStream();
        var connection = new SSEConnection(context, endpoint);
        endpoint.TrackConnection(connection);

        await connection.Send("failure", new { Value = 1 });
        await TestSseEndPoint.Closed.Task;

        Assert.Multiple(() =>
        {
            Assert.That(Connections(), Does.Not.Contain(connection));
            Assert.That(TestSseEndPoint.CloseCount, Is.EqualTo(1));
            Assert.That(connection.WaitForShutdown.IsCompleted, Is.True);
        });
    }

    [Test]
    public async Task Concurrent_removal_closes_an_sse_connection_only_once()
    {
        var endpoint = TestSseEndPoint.Instance!;
        var connection = new SSEConnection(
            CreateContext(CancellationToken.None),
            endpoint);
        endpoint.TrackConnection(connection);

        await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => Task.Run(() => endpoint.RemoveInstance(connection))));
        await TestSseEndPoint.Closed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(Connections(), Does.Not.Contain(connection));
            Assert.That(TestSseEndPoint.CloseCount, Is.EqualTo(1));
            Assert.That(connection.WaitForShutdown.IsCompleted, Is.True);
        });
    }

    [Test]
    public async Task Stop_signals_shutdown_to_every_tracked_connection()
    {
        var endpoint = TestSseEndPoint.Instance!;
        var first = new SSEConnection(
            CreateContext(CancellationToken.None),
            endpoint);
        var second = new SSEConnection(
            CreateContext(CancellationToken.None),
            endpoint);
        endpoint.TrackConnection(first);
        endpoint.TrackConnection(second);

        await endpoint.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(first.WaitForShutdown.IsCompleted, Is.True);
            Assert.That(second.WaitForShutdown.IsCompleted, Is.True);
        });
    }

    [Test]
    public async Task Typed_event_resolves_registered_endpoint_and_reports_missing_one()
    {
        var endpoint = TestSseEndPoint.Instance!;
        var context = CreateContext(CancellationToken.None);
        var connection = new SSEConnection(context, endpoint);
        endpoint.TrackConnection(connection);

        var emitted = await new StatusEvent(21)
            .EmitTo<TestSseEndPoint>();
        var missing = await new StatusEvent(22)
            .EmitTo<UnregisteredSseEndPoint>();

        var envelope = ReadSingleEvent(context);
        var data = JObject.Parse(envelope["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(emitted.Success, Is.True,
                ErrorMessages(emitted.Errors));
            Assert.That(data["Value"]?.Value<int>(), Is.EqualTo(21));
            Assert.That(missing.Success, Is.False);
            Assert.That(missing.Errors.OfType<SSEError>()
                    .Select(error => error.Code),
                Does.Contain(SSEErrorCode.NoEndPoint));
        });
    }

    [Test]
    public async Task Concurrent_connections_keep_context_scope_isolated()
    {
        TestSseEndPoint.SynchronizeOpen = true;
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var first = CreateContext(firstCancellation.Token);
        first.TraceIdentifier = "sse-first";
        var second = CreateContext(secondCancellation.Token);
        second.TraceIdentifier = "sse-second";

        var firstRequest = SSEMiddleware.OnRequest(
            first,
            () => Task.CompletedTask);
        var secondRequest = SSEMiddleware.OnRequest(
            second,
            () => Task.CompletedTask);
        await TestSseEndPoint.ScopeReady.Task;
        firstCancellation.Cancel();
        secondCancellation.Cancel();
        await Task.WhenAll(firstRequest, secondRequest);

        Assert.Multiple(() =>
        {
            Assert.That(TestSseEndPoint.ObservedScopes["sse-first"],
                Is.EqualTo("sse-first"));
            Assert.That(TestSseEndPoint.ObservedScopes["sse-second"],
                Is.EqualTo("sse-second"));
            Assert.That(AventusSharp.Routes.RouterMiddleware.AventusContextScope,
                Is.Null);
        });
    }

    [Test]
    public void Connection_uses_session_id_when_session_feature_exists()
    {
        var context = CreateContext(CancellationToken.None);
        context.Features.Set<ISessionFeature>(
            new TestSessionFeature(new TestSession("known-session")));

        var connection = new SSEConnection(
            context,
            TestSseEndPoint.Instance!);

        Assert.That(connection.SessionId, Is.EqualTo("known-session"));
    }

    [Test]
    public async Task Empty_event_emits_an_empty_json_object()
    {
        var context = CreateContext(CancellationToken.None);
        var connection = new SSEConnection(
            context,
            TestSseEndPoint.Instance!);

        await new EmptyStatusEvent().EmitTo(connection);

        var envelope = ReadSingleEvent(context);
        Assert.Multiple(() =>
        {
            Assert.That(envelope["channel"]?.Value<string>(),
                Is.EqualTo("empty-status"));
            Assert.That(JObject.Parse(
                envelope["data"]!.Value<string>()!), Is.Empty);
        });
    }

    private static List<SSEConnection> Connections()
    {
        return TestSseEndPoint.Instance!.GetConnectionsSnapshot();
    }

    private static string ErrorMessages(
        IEnumerable<AventusSharp.Tools.GenericError> errors) =>
        string.Join(Environment.NewLine,
            errors.Select(error => error.Message));

    private static DefaultHttpContext CreateContext(CancellationToken token)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/tests-sse";
        context.RequestAborted = token;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context) =>
        Encoding.UTF8.GetString(
            ((MemoryStream)context.Response.Body).ToArray());

    private static JObject ReadSingleEvent(HttpContext context)
    {
        var text = ReadBody(context);
        Assert.That(text, Does.StartWith("data: "));
        Assert.That(text, Does.EndWith("\n\n"));
        return JObject.Parse(text["data: ".Length..^2]);
    }

    public sealed class TestSseEndPoint : SSEEndPoint
    {
        public static TestSseEndPoint? Instance;
        public static bool Allow = true;
        public static int OpenCount;
        public static int CloseCount;
        public static SSEConnection? LastConnection;
        public static TaskCompletionSource Opened = NewSource();
        public static TaskCompletionSource Closed = NewSource();
        public static bool SynchronizeOpen;
        public static int ScopeCount;
        public static TaskCompletionSource ScopeReady = NewSource();
        public static ConcurrentDictionary<string, string?> ObservedScopes = [];
        public static bool ThrowOnOpen;
        public static bool ThrowOnClose;

        public TestSseEndPoint()
        {
            Instance = this;
        }

        private static TaskCompletionSource NewSource() => new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset(bool allow)
        {
            Allow = allow;
            OpenCount = 0;
            CloseCount = 0;
            LastConnection = null;
            Opened = NewSource();
            Closed = NewSource();
            SynchronizeOpen = false;
            ScopeCount = 0;
            ScopeReady = NewSource();
            ObservedScopes = [];
            ThrowOnOpen = false;
            ThrowOnClose = false;
        }

        public override string DefinePath() => "/tests-sse";
        public override bool Main() => true;

        public override bool CanOpenConnection(HttpContext context) => Allow;

        protected override async Task OnConnectionOpen(SSEConnection connection)
        {
            OpenCount++;
            LastConnection = connection;
            Opened.TrySetResult();
            if (ThrowOnOpen)
                throw new InvalidOperationException("open callback failure");
            if (SynchronizeOpen)
            {
                if (Interlocked.Increment(ref ScopeCount) == 2)
                    ScopeReady.TrySetResult();
                await ScopeReady.Task;
                await Task.Yield();
                var key = connection.GetContext().TraceIdentifier;
                ObservedScopes[key] =
                    AventusSharp.AspNetCore.Hosting.AspNetCoreContextAccessor
                        .Current?.TraceIdentifier;
            }
        }

        protected override Task OnConnectionClose(SSEConnection connection)
        {
            CloseCount++;
            Closed.TrySetResult();
            if (ThrowOnClose)
                throw new InvalidOperationException("close callback failure");
            return Task.CompletedTask;
        }
    }

    public sealed class UnregisteredSseEndPoint : SSEEndPoint
    {
        public override string DefinePath() => "/unregistered-sse";
    }

    private sealed class StatusEvent(int value) : SSEEvent<object>
    {
        protected override string GetTopic() => "status-event";
        protected override object GetBody() => new { Value = value };
    }

    private sealed class EmptyStatusEvent : SSEEmptyEvent
    {
        protected override string GetTopic() => "empty-status";
    }

    private sealed class TestSessionFeature(ISession session)
        : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestSession(string id) : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public bool IsAvailable => true;
        public string Id { get; } = id;
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) =>
            values.TryGetValue(key, out value!);
    }

    private sealed class ThrowingWriteStream : MemoryStream
    {
        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            Task.FromException(new IOException("intentional SSE write failure"));

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(
                new IOException("intentional SSE write failure"));
    }
}
