using AventusSharp.WebSocket;
using AventusSharp.WebSocket.Attributes;
using AventusSharp.WebSocket.Event;
using AventusSharp.WebSocket.Request;
using AventusSharp.Routes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using WsPath = AventusSharp.WebSocket.Attributes.Path;

namespace AventusSharpTest.WebSocket;

[TestFixture]
[NonParallelizable]
public sealed class WebSocketRoutingTests
{
    [OneTimeSetUp]
    public void RegisterWebSocketTypes()
    {
        var result = WebSocketMiddleware.Register(
            new[] { typeof(TestEndPoint), typeof(LifecycleEndPoint) },
            new[] { typeof(TestWsRouter) });

        Assert.That(result.Success, Is.True,
            string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }

    [Test]
    public void Route_listing_maps_router_to_endpoint()
    {
        var endpoint = WebSocketMiddleware.GetAllRoutes()
            .Single(pair => pair.Key is TestEndPoint);

        Assert.That(endpoint.Key.Path, Is.EqualTo("/tests-ws"));
        Assert.That(endpoint.Value, Has.Count.EqualTo(13));
        Assert.That(endpoint.Value.Any(route => route.baseUrl == "/devices/{id}"), Is.True);
    }

    [Test]
    public void Route_metadata_contains_parameter_positions_and_response_type()
    {
        var route = WebSocketMiddleware.GetAllRoutes()
            .Single(pair => pair.Key is TestEndPoint)
            .Value.Single(value => value.baseUrl == "/devices/{id}");

        Assert.That(route.parameters["id"].positionUrl, Is.EqualTo(0));
        Assert.That(route.parameters["id"].positionCSharp, Is.EqualTo(0));
        Assert.That(route.eventType, Is.EqualTo(ResponseTypeEnum.Single));
    }

    [Test]
    public async Task Route_binds_path_parameter_and_preserves_request_uid()
    {
        var (endpoint, connection, socket) = CreateConnection();

        await endpoint.Route(
            connection,
            "/devices/42",
            new WebSocketRouterBody(null),
            "request-42");

        var message = SingleMessage(socket);
        var data = JObject.Parse(message["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(message["channel"]?.Value<string>(),
                Is.EqualTo("/devices/42"));
            Assert.That(message["uid"]?.Value<string>(),
                Is.EqualTo("request-42"));
            Assert.That(data["Id"]?.Value<int>(), Is.EqualTo(42));
            Assert.That(RouterMiddleware.AventusContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Route_binds_json_body_and_serializes_plain_result()
    {
        var (endpoint, connection, socket) = CreateConnection();

        await endpoint.Route(
            connection,
            "/sum",
            new WebSocketRouterBody(
                """{"body":{"left":4,"right":7}}"""),
            "sum-request");

        var message = SingleMessage(socket);
        var data = JObject.Parse(message["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(message["uid"]?.Value<string>(),
                Is.EqualTo("sum-request"));
            Assert.That(data["Result"]?.Value<int>(), Is.EqualTo(11));
        });
    }

    [Test]
    public async Task Route_injects_known_websocket_parameters()
    {
        var (endpoint, connection, socket) = CreateConnection();

        await endpoint.Route(
            connection,
            "/known",
            new WebSocketRouterBody(null));

        var data = JObject.Parse(
            SingleMessage(socket)["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(data["Connection"]?.Value<bool>(), Is.True);
            Assert.That(data["Endpoint"]?.Value<bool>(), Is.True);
            Assert.That(data["Context"]?.Value<bool>(), Is.True);
            Assert.That(data["Socket"]?.Value<bool>(), Is.True);
        });
    }

    [Test]
    public async Task Endpoint_middleware_can_short_circuit_and_preserve_uid()
    {
        var (endpoint, connection, socket) = CreateConnection();

        await endpoint.Route(
            connection,
            "/blocked",
            new WebSocketRouterBody(null),
            "blocked-request");

        var message = SingleMessage(socket);
        var data = JObject.Parse(message["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(message["uid"]?.Value<string>(),
                Is.EqualTo("blocked-request"));
            Assert.That(data["Blocked"]?.Value<bool>(), Is.True);
            Assert.That(TestWsRouter.BlockedInvocations, Is.Zero);
            Assert.That(RouterMiddleware.AventusContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Unknown_route_sends_nothing_and_clears_context_scope()
    {
        var (endpoint, connection, socket) = CreateConnection();

        await endpoint.Route(
            connection,
            "/unknown",
            new WebSocketRouterBody(null),
            "unknown-request");

        Assert.Multiple(() =>
        {
            Assert.That(socket.Messages, Is.Empty);
            Assert.That(RouterMiddleware.AventusContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Missing_body_returns_ws_error_and_preserves_request_uid()
    {
        var (endpoint, connection, socket) = CreateConnection();

        await endpoint.Route(
            connection,
            "/sum",
            new WebSocketRouterBody("{}"),
            "missing-body-request");

        var message = SingleMessage(socket);
        var data = JObject.Parse(message["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(message["uid"]?.Value<string>(),
                Is.EqualTo("missing-body-request"));
            Assert.That(data["Errors"]?[0]?["Code"]?.Value<int>(),
                Is.EqualTo((int)WsErrorCode.CantGetValueFromBody));
            Assert.That(RouterMiddleware.AventusContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Broadcast_route_sends_to_every_connection()
    {
        var sender = CreateConnection();
        var second = CreateConnection();
        var third = CreateConnection();
        var connections = TrackConnections(
            sender.Endpoint,
            sender.Connection,
            second.Connection,
            third.Connection);
        try
        {
            await sender.Endpoint.Route(
                sender.Connection,
                "/broadcast",
                new WebSocketRouterBody(null),
                "broadcast-request");

            AssertBroadcastMessage(sender.Socket, "Broadcast",
                "broadcast-request");
            AssertBroadcastMessage(second.Socket, "Broadcast",
                "broadcast-request");
            AssertBroadcastMessage(third.Socket, "Broadcast",
                "broadcast-request");
        }
        finally
        {
            connections.Clear();
        }
    }

    [Test]
    public async Task Others_route_omits_the_requesting_connection()
    {
        var sender = CreateConnection();
        var second = CreateConnection();
        var third = CreateConnection();
        var connections = TrackConnections(
            sender.Endpoint,
            sender.Connection,
            second.Connection,
            third.Connection);
        try
        {
            await sender.Endpoint.Route(
                sender.Connection,
                "/others",
                new WebSocketRouterBody(null),
                "others-request");

            Assert.That(sender.Socket.Messages, Is.Empty);
            AssertBroadcastMessage(second.Socket, "Others",
                "others-request");
            AssertBroadcastMessage(third.Socket, "Others",
                "others-request");
        }
        finally
        {
            connections.Clear();
        }
    }

    [Test]
    public async Task Custom_route_uses_the_attribute_connection_selection()
    {
        var sender = CreateConnection();
        var second = CreateConnection();
        var selected = CreateConnection();
        var connections = TrackConnections(
            sender.Endpoint,
            sender.Connection,
            second.Connection,
            selected.Connection);
        OnlySelectedAttribute.Targets = [selected.Connection];
        try
        {
            await sender.Endpoint.Route(
                sender.Connection,
                "/custom",
                new WebSocketRouterBody(null),
                "custom-request");

            Assert.That(sender.Socket.Messages, Is.Empty);
            Assert.That(second.Socket.Messages, Is.Empty);
            AssertBroadcastMessage(selected.Socket, "Custom",
                "custom-request");
        }
        finally
        {
            OnlySelectedAttribute.Targets = [];
            connections.Clear();
        }
    }

    [Test]
    public async Task Broadcast_removes_closed_connection_and_continues_with_open_ones()
    {
        var closed = CreateConnection();
        var open = CreateConnection();
        closed.Socket.Abort();
        var tracked = TrackConnections(
            open.Endpoint,
            closed.Connection,
            open.Connection);
        try
        {
            await open.Endpoint.Broadcast(
                    "state",
                    new { Value = 5 })
                .WaitAsync(TimeSpan.FromSeconds(2));

            Assert.That(
                SpinWait.SpinUntil(
                    () => !open.Endpoint.GetConnectionsSnapshot()
                        .Contains(closed.Connection),
                    TimeSpan.FromSeconds(2)),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(closed.Socket.Messages, Is.Empty);
                Assert.That(open.Socket.Messages, Has.Count.EqualTo(1));
                Assert.That(
                    JObject.Parse(open.Socket.Messages.Single())["channel"]?
                        .Value<string>(),
                    Is.EqualTo("state"));
            });
        }
        finally
        {
            tracked.Clear();
        }
    }

    [Test]
    public async Task Concurrent_routes_keep_context_scope_isolated()
    {
        TestWsRouter.ScopeReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TestWsRouter.ScopeCount = 0;
        var first = CreateConnection("/socket-context/first");
        var second = CreateConnection("/socket-context/second");

        await Task.WhenAll(
            first.Endpoint.Route(
                first.Connection,
                "/scope/first",
                new WebSocketRouterBody(null),
                "scope-first"),
            second.Endpoint.Route(
                second.Connection,
                "/scope/second",
                new WebSocketRouterBody(null),
                "scope-second"));

        var firstData = JObject.Parse(
            SingleMessage(first.Socket)["data"]!.Value<string>()!);
        var secondData = JObject.Parse(
            SingleMessage(second.Socket)["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(firstData["Id"]?.Value<string>(),
                Is.EqualTo("first"));
            Assert.That(firstData["Scope"]?.Value<string>(),
                Is.EqualTo("/socket-context/first"));
            Assert.That(secondData["Id"]?.Value<string>(),
                Is.EqualTo("second"));
            Assert.That(secondData["Scope"]?.Value<string>(),
                Is.EqualTo("/socket-context/second"));
            Assert.That(RouterMiddleware.AventusContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Middleware_opens_tracks_and_closes_an_accepted_connection()
    {
        LifecycleEndPoint.Reset(allow: true);
        var socket = new CapturingWebSocket();
        var context = CreateWebSocketContext("/lifecycle-ws", socket);
        var nextCalled = false;

        await WebSocketMiddleware.OnRequest(context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        await LifecycleEndPoint.Closed.Task;

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(LifecycleEndPoint.OpenCount, Is.EqualTo(1));
            Assert.That(LifecycleEndPoint.CloseCount, Is.EqualTo(1));
            Assert.That(LifecycleEndPoint.LookupSucceeded, Is.True);
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Closed));
            Assert.That(WebSocketMiddleware
                .GetConnection<LifecycleEndPoint>(
                    LifecycleEndPoint.LastSessionId), Is.Null);
        });
    }

    [Test]
    public async Task Endpoint_can_refuse_connection_before_open_callback()
    {
        LifecycleEndPoint.Reset(allow: false);
        var socket = new CapturingWebSocket();
        var context = CreateWebSocketContext("/lifecycle-ws", socket);

        await WebSocketMiddleware.OnRequest(
            context,
            () => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(302));
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Aborted));
            Assert.That(LifecycleEndPoint.OpenCount, Is.Zero);
            Assert.That(LifecycleEndPoint.CloseCount, Is.Zero);
        });
    }

    [Test]
    public async Task Open_callback_failure_still_runs_the_connection_cleanup()
    {
        LifecycleEndPoint.Reset(allow: true);
        LifecycleEndPoint.ThrowOnOpen = true;
        var socket = new CapturingWebSocket();
        var context = CreateWebSocketContext("/lifecycle-ws", socket);

        Assert.DoesNotThrowAsync(async () =>
            await WebSocketMiddleware.OnRequest(
                context,
                () => Task.CompletedTask));
        await LifecycleEndPoint.Closed.Task;

        Assert.Multiple(() =>
        {
            Assert.That(LifecycleEndPoint.OpenCount, Is.EqualTo(1));
            Assert.That(LifecycleEndPoint.CloseCount, Is.EqualTo(1));
            Assert.That(WebSocketMiddleware
                .GetConnection<LifecycleEndPoint>(
                    LifecycleEndPoint.LastSessionId), Is.Null);
        });
    }

    [Test]
    public async Task Concurrent_removal_closes_a_websocket_connection_only_once()
    {
        LifecycleEndPoint.Reset(allow: true);
        var endpoint = (LifecycleEndPoint)
            WebSocketMiddleware.endPointInstances["/lifecycle-ws"];
        var socket = new CapturingWebSocket();
        var context = CreateWebSocketContext("/lifecycle-ws", socket);
        var connection = new WebSocketConnection(context, socket, endpoint);
        endpoint.TrackConnection(connection);

        Parallel.Invoke(
            () => endpoint.RemoveInstance(connection),
            () => endpoint.RemoveInstance(connection));
        await LifecycleEndPoint.Closed.Task;
        Assert.That(
            SpinWait.SpinUntil(
                () => endpoint.GetConnectionsSnapshot().Count == 0,
                TimeSpan.FromSeconds(2)),
            Is.True);

        Assert.That(LifecycleEndPoint.CloseCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Close_callback_failure_does_not_leave_the_connection_tracked()
    {
        LifecycleEndPoint.Reset(allow: true);
        LifecycleEndPoint.ThrowOnClose = true;
        var endpoint = (LifecycleEndPoint)
            WebSocketMiddleware.endPointInstances["/lifecycle-ws"];
        var socket = new CapturingWebSocket();
        var context = CreateWebSocketContext("/lifecycle-ws", socket);
        var connection = new WebSocketConnection(context, socket, endpoint);
        endpoint.TrackConnection(connection);

        Assert.DoesNotThrow(() => endpoint.RemoveInstance(connection));
        await LifecycleEndPoint.Closed.Task;

        Assert.Multiple(() =>
        {
            Assert.That(LifecycleEndPoint.CloseCount, Is.EqualTo(1));
            Assert.That(endpoint.GetConnectionsSnapshot(), Is.Empty);
        });
    }

    [Test]
    public async Task Unknown_websocket_endpoint_calls_next_without_accepting()
    {
        var socket = new CapturingWebSocket();
        var context = CreateWebSocketContext("/unknown-ws", socket);
        var feature = (TestWebSocketFeature)context.Features
            .Get<IHttpWebSocketFeature>()!;
        var nextCalled = false;

        await WebSocketMiddleware.OnRequest(context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(feature.AcceptCount, Is.Zero);
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Open));
        });
    }

    [TestCase("/throw-aventus", "expected websocket failure")]
    [TestCase("/throw-async", "unexpected websocket failure")]
    public async Task Route_exception_returns_ws_error_preserves_uid_and_clears_scope(
        string path,
        string expectedMessage)
    {
        var (endpoint, connection, socket) = CreateConnection();

        Assert.DoesNotThrowAsync(async () => await endpoint.Route(
            connection,
            path,
            new WebSocketRouterBody(null),
            "error-request"));

        var message = SingleMessage(socket);
        var data = JObject.Parse(message["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(message["uid"]?.Value<string>(),
                Is.EqualTo("error-request"));
            Assert.That(data["Errors"]?[0]?["Code"]?.Value<int>(),
                Is.EqualTo((int)WsErrorCode.UnknownError));
            Assert.That(data["Errors"]?[0]?["Message"]?.Value<string>(),
                Does.Contain(expectedMessage));
            Assert.That(RouterMiddleware.AventusContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Connection_reassembles_fragmented_message_and_routes_it()
    {
        var (_, connection, socket) = CreateConnection();
        const string message =
            """{"channel":"/devices/73","data":{},"uid":"fragmented"}""";
        var split = message.Length / 2;
        socket.EnqueueText(message[..split], endOfMessage: false);
        socket.EnqueueText(message[split..], endOfMessage: true);

        await connection.Start();

        var response = SingleMessage(socket);
        var data = JObject.Parse(response["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(response["uid"]?.Value<string>(),
                Is.EqualTo("fragmented"));
            Assert.That(data["Id"]?.Value<int>(), Is.EqualTo(73));
            Assert.That(socket.State, Is.EqualTo(WebSocketState.Closed));
        });
    }

    [Test]
    public async Task Connection_answers_ping_with_pong()
    {
        var (_, connection, socket) = CreateConnection();
        socket.EnqueueText(
            """{"channel":"ping","data":{},"uid":"ignored"}""",
            endOfMessage: true);

        await connection.Start();

        var response = SingleMessage(socket);
        Assert.Multiple(() =>
        {
            Assert.That(response["channel"]?.Value<string>(),
                Is.EqualTo("pong"));
            Assert.That(response["uid"], Is.Null);
            Assert.That(JObject.Parse(
                response["data"]!.Value<string>()!), Is.Empty);
        });
    }

    [Test]
    public async Task Invalid_message_does_not_poison_the_next_valid_message()
    {
        var (_, connection, socket) = CreateConnection();
        socket.EnqueueText("{invalid", endOfMessage: true);
        socket.EnqueueText(
            """{"channel":"/devices/9","data":{},"uid":"after-invalid"}""",
            endOfMessage: true);

        await connection.Start();

        var response = SingleMessage(socket);
        var data = JObject.Parse(response["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(response["uid"]?.Value<string>(),
                Is.EqualTo("after-invalid"));
            Assert.That(data["Id"]?.Value<int>(), Is.EqualTo(9));
        });
    }

    [Test]
    public async Task Route_resolves_global_and_request_scoped_dependencies()
    {
        WebSocketMiddleware.Inject(
            typeof(IGlobalWsDependency),
            new GlobalWsDependency("global"));
        var services = new ServiceCollection()
            .AddSingleton(new ScopedWsDependency("scoped"))
            .BuildServiceProvider();
        var global = CreateConnection();
        var scoped = CreateConnection(services: services);

        await global.Endpoint.Route(
            global.Connection,
            "/global-service",
            new WebSocketRouterBody(null));
        await scoped.Endpoint.Route(
            scoped.Connection,
            "/scoped-service",
            new WebSocketRouterBody(null));

        var globalData = JObject.Parse(
            SingleMessage(global.Socket)["data"]!.Value<string>()!);
        var scopedData = JObject.Parse(
            SingleMessage(scoped.Socket)["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(globalData["Value"]?.Value<string>(),
                Is.EqualTo("global"));
            Assert.That(scopedData["Value"]?.Value<string>(),
                Is.EqualTo("scoped"));
        });
    }

    private static DefaultHttpContext CreateWebSocketContext(
        string path,
        CapturingWebSocket socket)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.RequestServices =
            new ServiceCollection().BuildServiceProvider();
        context.Features.Set<ISessionFeature>(
            new TestSessionFeature(new TestSession()));
        context.Features.Set<IHttpWebSocketFeature>(
            new TestWebSocketFeature(socket));
        return context;
    }

    private static System.Collections.Concurrent.ConcurrentDictionary<WebSocketConnection, byte> TrackConnections(
        TestEndPoint endpoint,
        params WebSocketConnection[] connections)
    {
        var tracked = endpoint.connections;
        tracked.Clear();
        foreach (var connection in connections)
        {
            endpoint.TrackConnection(connection);
        }
        return tracked;
    }

    private static void AssertBroadcastMessage(
        CapturingWebSocket socket,
        string expectedMode,
        string expectedUid)
    {
        var message = SingleMessage(socket);
        var data = JObject.Parse(message["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(data["Mode"]?.Value<string>(),
                Is.EqualTo(expectedMode));
            Assert.That(message["uid"]?.Value<string>(),
                Is.EqualTo(expectedUid));
        });
    }

    private static (TestEndPoint Endpoint, WebSocketConnection Connection,
        CapturingWebSocket Socket) CreateConnection(
            string contextPath = "/tests-ws",
            IServiceProvider? services = null)
    {
        var endpoint = (TestEndPoint)WebSocketMiddleware.GetAllRoutes()
            .Keys.Single(value => value is TestEndPoint);
        var context = new DefaultHttpContext();
        context.Request.Path = contextPath;
        context.RequestServices = services ??
            new ServiceCollection().BuildServiceProvider();
        context.Features.Set<ISessionFeature>(
            new TestSessionFeature(new TestSession()));
        var socket = new CapturingWebSocket();
        var connection = new WebSocketConnection(context, socket, endpoint);
        return (endpoint, connection, socket);
    }

    private static JObject SingleMessage(CapturingWebSocket socket)
    {
        Assert.That(socket.Messages, Has.Count.EqualTo(1));
        return JObject.Parse(socket.Messages.Single());
    }

    public sealed class TestEndPoint : WsEndPoint
    {
        public TestEndPoint()
        {
            Use(async (connection, path, body, uid) =>
            {
                if (path != "/blocked")
                    return true;
                await connection.Send(
                    path,
                    new { Blocked = true },
                    uid);
                return false;
            });
        }

        public override string DefinePath() => "/tests-ws";
    }

    public sealed class LifecycleEndPoint : WsEndPoint
    {
        public static bool Allow;
        public static int OpenCount;
        public static int CloseCount;
        public static bool LookupSucceeded;
        public static string LastSessionId = "";
        public static TaskCompletionSource Closed = NewSource();
        public static bool ThrowOnOpen;
        public static bool ThrowOnClose;

        private static TaskCompletionSource NewSource() => new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset(bool allow)
        {
            Allow = allow;
            OpenCount = 0;
            CloseCount = 0;
            LookupSucceeded = false;
            LastSessionId = "";
            Closed = NewSource();
            ThrowOnOpen = false;
            ThrowOnClose = false;
        }

        public override string DefinePath() => "/lifecycle-ws";

        public override bool CanOpenConnection(
            HttpContext context,
            System.Net.WebSockets.WebSocket webSocket) => Allow;

        protected override Task OnConnectionOpen(
            WebSocketConnection connection)
        {
            OpenCount++;
            LastSessionId = connection.SessionId;
            LookupSucceeded = ReferenceEquals(
                WebSocketMiddleware.GetConnection<LifecycleEndPoint>(
                    connection.SessionId),
                connection);
            if (ThrowOnOpen)
                throw new InvalidOperationException("open callback failure");
            return Task.CompletedTask;
        }

        protected override Task OnConnectionClose(
            WebSocketConnection connection)
        {
            CloseCount++;
            Closed.TrySetResult();
            if (ThrowOnClose)
                throw new InvalidOperationException("close callback failure");
            return Task.CompletedTask;
        }
    }

    [EndPoint<TestEndPoint>]
    public sealed class TestWsRouter : WsRouter
    {
        public static int BlockedInvocations;
        public static TaskCompletionSource ScopeReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public static int ScopeCount;

        [WsPath("/devices/{id}")]
        public WebSocketEvent GetDevice(int id) => new JsonEvent(new { Id = id });

        [WsPath("/ping")]
        public WebSocketEvent Ping() => new EmptyEvent();

        [WsPath("/sum")]
        public object Sum(SumBody body) =>
            new { Result = body.Left + body.Right };

        [WsPath("/known")]
        public object Known(
            WebSocketConnection connection,
            WsEndPoint endpoint,
            HttpContext context,
            System.Net.WebSockets.WebSocket socket) => new
            {
                Connection = connection != null,
                Endpoint = endpoint != null,
                Context = context != null,
                Socket = socket != null
            };

        [WsPath("/blocked")]
        public object Blocked()
        {
            BlockedInvocations++;
            return new { Executed = true };
        }

        [WsPath("/broadcast")]
        [Broadcast]
        public object BroadcastToAll() => new { Mode = "Broadcast" };

        [WsPath("/others")]
        [Others]
        public object BroadcastToOthers() => new { Mode = "Others" };

        [WsPath("/custom")]
        [OnlySelected]
        public object BroadcastToSelected() => new { Mode = "Custom" };

        [WsPath("/scope/{id}")]
        public async Task<object> ConcurrentScope(string id)
        {
            if (Interlocked.Increment(ref ScopeCount) == 2)
                ScopeReady.SetResult();
            await ScopeReady.Task;
            await Task.Yield();
            return new
            {
                Id = id,
                Scope = RouterMiddleware.AventusContextScope?.Request.Path.ToString()
            };
        }

        [WsPath("/throw-aventus")]
        public object ThrowAventus() =>
            throw new WsError(
                WsErrorCode.UnknownError,
                "expected websocket failure").GetException();

        [WsPath("/throw-async")]
        public async Task<object> ThrowAsync()
        {
            await Task.Yield();
            throw new InvalidOperationException(
                "unexpected websocket failure");
        }

        [WsPath("/global-service")]
        public object GlobalService(IGlobalWsDependency dependency) =>
            new { dependency.Value };

        [WsPath("/scoped-service")]
        public object ScopedService(ScopedWsDependency dependency) =>
            new { dependency.Value };
    }

    public sealed class SumBody
    {
        public int Left { get; set; }
        public int Right { get; set; }
    }

    public interface IGlobalWsDependency
    {
        string Value { get; }
    }

    public sealed record GlobalWsDependency(string Value)
        : IGlobalWsDependency;

    public sealed record ScopedWsDependency(string Value);

    public sealed class OnlySelectedAttribute : Custom
    {
        public static List<WebSocketConnection> Targets { get; set; } = [];

        public override List<WebSocketConnection> GetConnections(
            WsEndPoint endPoint,
            WebSocketConnection? connection) => Targets;
    }

    private sealed class TestSessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
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

    private sealed class TestWebSocketFeature(
        System.Net.WebSockets.WebSocket socket)
        : IHttpWebSocketFeature
    {
        public int AcceptCount { get; private set; }
        public bool IsWebSocketRequest => true;

        public Task<System.Net.WebSockets.WebSocket> AcceptAsync(
            WebSocketAcceptContext context)
        {
            AcceptCount++;
            return Task.FromResult(socket);
        }
    }

    private sealed class CapturingWebSocket : System.Net.WebSockets.WebSocket
    {
        private WebSocketState state = WebSocketState.Open;
        private readonly Queue<ReceiveFrame> receiveFrames = [];
        public List<string> Messages { get; } = [];

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;

        public override void Abort() => state = WebSocketState.Aborted;

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose() => state = WebSocketState.Closed;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            if (receiveFrames.Count == 0)
            {
                return Task.FromResult(new WebSocketReceiveResult(
                    0,
                    WebSocketMessageType.Close,
                    true,
                    WebSocketCloseStatus.NormalClosure,
                    null));
            }

            var frame = receiveFrames.Dequeue();
            frame.Bytes.CopyTo(
                buffer.Array!,
                buffer.Offset);
            return Task.FromResult(new WebSocketReceiveResult(
                frame.Bytes.Length,
                WebSocketMessageType.Text,
                frame.EndOfMessage));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            Messages.Add(Encoding.UTF8.GetString(
                buffer.Array!,
                buffer.Offset,
                buffer.Count));
            return Task.CompletedTask;
        }

        public void EnqueueText(string text, bool endOfMessage)
        {
            receiveFrames.Enqueue(new ReceiveFrame(
                Encoding.UTF8.GetBytes(text),
                endOfMessage));
        }

        private sealed record ReceiveFrame(
            byte[] Bytes,
            bool EndOfMessage);
    }
}
