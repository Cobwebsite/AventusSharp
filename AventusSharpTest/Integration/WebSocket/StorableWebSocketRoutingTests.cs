using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using AventusSharp.Data.Manager;
using AventusSharp.Routes;
using AventusSharp.WebSocket;
using AventusSharp.WebSocket.Attributes;
using AventusSharp.WebSocket.Request;
using AventusSharpTest.Integration.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace AventusSharpTest.Integration.WebSocket;

[TestFixture]
[NonParallelizable]
public sealed class StorableWebSocketRoutingTests
{
    [OneTimeSetUp]
    public void RegisterRouter()
    {
        var result = WebSocketMiddleware.Register(
            [typeof(DeviceWsEndPoint)],
            [typeof(DeviceWsRouter)]);
        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [SetUp]
    public async Task ClearDevices()
    {
        var result = await Device.StartDelete()
            .Where(device => device.Id > 0)
            .RunWithError();
        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Connections().Clear();
    }

    [TearDown]
    public void ClearConnections() => Connections().Clear();

    [Test]
    public void Storable_ws_router_registers_crud_and_bulk_routes()
    {
        var endpoint = Endpoint();
        var routes = WebSocketMiddleware.GetAllRoutes()[endpoint];

        Assert.Multiple(() =>
        {
            Assert.That(routes, Has.Count.EqualTo(9));
            AssertRoute(routes, "/[StorableName]", "/ws-device");
            AssertRoute(routes, "/[StorableName]/Create", "/ws-device/create");
            AssertRoute(routes, "/[StorableName]/CreateMany", "/ws-device/createmany");
            AssertRoute(routes, "/[StorableName]/{id}", "/ws-device/1");
            AssertRoute(routes, "/[StorableName]/getbyids", "/ws-device/getbyids");
            AssertRoute(routes, "/[StorableName]/{id}/Update", "/ws-device/1/update");
            AssertRoute(routes, "/[StorableName]/UpdateMany", "/ws-device/updatemany");
            AssertRoute(routes, "/[StorableName]/{id}/Delete", "/ws-device/1/delete");
            AssertRoute(routes, "/[StorableName]/DeleteMany", "/ws-device/deletemany");
        });
    }

    [Test]
    public async Task Storable_ws_router_executes_single_item_crud_lifecycle()
    {
        var (endpoint, connection, socket) = CreateConnection();
        Endpoint().TrackConnection(connection);

        await endpoint.Route(
            connection,
            "/ws-device/Create",
            new WebSocketRouterBody(
                """
                {
                  "item": {
                    "Name": "WS lamp",
                    "Room": "Office",
                    "Brightness": 20,
                    "IsOnline": true
                  }
                }
                """),
            "create-request");
        var created = ReadData(socket);
        var id = created["Result"]?["Id"]?.Value<int>() ?? 0;
        socket.ClearMessages();

        await endpoint.Route(
            connection,
            $"/ws-device/{id}",
            new WebSocketRouterBody(null),
            "get-request");
        var loaded = ReadData(socket);
        socket.ClearMessages();

        await endpoint.Route(
            connection,
            $"/ws-device/{id}/Update",
            new WebSocketRouterBody(
                """
                {
                  "item": {
                    "Id": 999999,
                    "Name": "WS lamp updated",
                    "Room": "Office",
                    "Brightness": 80,
                    "IsOnline": false
                  }
                }
                """),
            "update-request");
        var updated = ReadData(socket);
        socket.ClearMessages();

        await endpoint.Route(
            connection,
            $"/ws-device/{id}/Delete",
            new WebSocketRouterBody(null),
            "delete-request");
        var deleted = ReadData(socket);
        var afterDelete = await Device.GetByIdWithError(id);

        Assert.Multiple(() =>
        {
            Assert.That(created["Success"]?.Value<bool>(), Is.True);
            Assert.That(id, Is.GreaterThan(0));
            Assert.That(loaded["Result"]?["Name"]?.Value<string>(),
                Is.EqualTo("WS lamp"));
            Assert.That(updated["Success"]?.Value<bool>(), Is.True);
            Assert.That(updated["Result"]?["Id"]?.Value<int>(),
                Is.EqualTo(id));
            Assert.That(updated["Result"]?["Name"]?.Value<string>(),
                Is.EqualTo("WS lamp updated"));
            Assert.That(deleted["Success"]?.Value<bool>(), Is.True);
            Assert.That(deleted["Result"]?["Id"]?.Value<int>(),
                Is.EqualTo(id));
            Assert.That(afterDelete.Result, Is.Null);
        });
    }

    [Test]
    public async Task Storable_ws_router_executes_bulk_and_get_by_ids_routes()
    {
        var (endpoint, connection, socket) = CreateConnection();
        Endpoint().TrackConnection(connection);

        await endpoint.Route(
            connection,
            "/ws-device/CreateMany",
            new WebSocketRouterBody(
                """
                {
                  "list": [
                    { "Name": "WS Alpha", "Room": "Lab", "Brightness": 10 },
                    { "Name": "WS Beta", "Room": "Lab", "Brightness": 20 }
                  ]
                }
                """),
            "create-many");
        var created = ReadData(socket);
        var ids = created["Result"]!
            .Select(item => item["Id"]!.Value<int>())
            .ToArray();
        socket.ClearMessages();

        await endpoint.Route(
            connection,
            "/ws-device/getbyids",
            new WebSocketRouterBody(
                $$"""{"ids":[{{ids[1]}},{{ids[0]}}]}"""),
            "get-by-ids");
        var byIds = ReadData(socket);
        socket.ClearMessages();

        await endpoint.Route(
            connection,
            "/ws-device/UpdateMany",
            new WebSocketRouterBody(
                $$"""
                {
                  "list": [
                    { "Id": {{ids[0]}}, "Name": "WS Alpha updated", "Room": "Lab", "Brightness": 30 },
                    { "Id": {{ids[1]}}, "Name": "WS Beta updated", "Room": "Lab", "Brightness": 40 }
                  ]
                }
                """),
            "update-many");
        var updated = ReadData(socket);
        socket.ClearMessages();

        await endpoint.Route(
            connection,
            "/ws-device/DeleteMany",
            new WebSocketRouterBody(
                $$"""{"ids":[{{ids[0]}},{{ids[1]}}]}"""),
            "delete-many");
        var deleted = ReadData(socket);

        Assert.Multiple(() =>
        {
            Assert.That(ids, Has.Length.EqualTo(2));
            Assert.That(byIds["Result"]!
                    .Select(item => item["Id"]!.Value<int>()),
                Is.EquivalentTo(ids));
            Assert.That(updated["Result"]!
                    .Select(item => item["Name"]!.Value<string>()),
                Is.EquivalentTo(new[]
                {
                    "WS Alpha updated",
                    "WS Beta updated"
                }));
            Assert.That(deleted["Result"]!
                    .Select(item => item.Value<int>()),
                Is.EquivalentTo(ids));
        });
    }

    [Test]
    public async Task External_manager_changes_emit_storable_events()
    {
        var (_, connection, socket) = CreateConnection();
        Endpoint().TrackConnection(connection);
        var device = new Device
        {
            Name = "External WS device",
            Room = "Automation",
            Brightness = 10,
            IsOnline = true
        };

        var creation = await Device.CreateWithError(device);
        await socket.WaitForMessage();
        var createdMessage = ReadMessage(socket);
        socket.ClearMessages();

        device.Brightness = 70;
        var update = await Device.UpdateWithError(device);
        await socket.WaitForMessage();
        var updatedMessage = ReadMessage(socket);
        socket.ClearMessages();

        var deletion = await Device.DeleteWithError(device);
        await socket.WaitForMessage();
        var deletedMessage = ReadMessage(socket);

        var createdData = JObject.Parse(
            createdMessage["data"]!.Value<string>()!);
        var updatedData = JObject.Parse(
            updatedMessage["data"]!.Value<string>()!);
        var deletedData = JObject.Parse(
            deletedMessage["data"]!.Value<string>()!);
        Assert.Multiple(() =>
        {
            Assert.That(creation.Success, Is.True);
            Assert.That(update.Success, Is.True);
            Assert.That(deletion.Success, Is.True);
            Assert.That(createdMessage["channel"]?.Value<string>(),
                Is.EqualTo("/ws-device/CreateMany"));
            Assert.That(createdData["Result"]?[0]?["Id"]?.Value<int>(),
                Is.EqualTo(device.Id));
            Assert.That(updatedMessage["channel"]?.Value<string>(),
                Is.EqualTo("/ws-device/UpdateMany"));
            Assert.That(updatedData["Result"]?[0]?["Brightness"]?.Value<int>(),
                Is.EqualTo(70));
            Assert.That(deletedMessage["channel"]?.Value<string>(),
                Is.EqualTo("/ws-device/DeleteMany"));
            Assert.That(deletedData["Result"]?[0]?.Value<int>(),
                Is.EqualTo(device.Id));
        });
    }

    private static void AssertRoute(
        IEnumerable<WebSocketRouteInfo> routes,
        string template,
        string resolvedPath)
    {
        Assert.That(routes.Any(route =>
            route.baseUrl == template &&
            route.pattern.IsMatch(resolvedPath)), Is.True,
            $"Missing {template} ({resolvedPath}).");
    }

    private static JObject ReadData(CapturingWebSocket socket)
    {
        var message = ReadMessage(socket);
        return JObject.Parse(message["data"]!.Value<string>()!);
    }

    private static JObject ReadMessage(CapturingWebSocket socket)
    {
        Assert.That(socket.Messages, Has.Count.EqualTo(1));
        return JObject.Parse(socket.Messages.Single());
    }

    private static DeviceWsEndPoint Endpoint() =>
        (DeviceWsEndPoint)WebSocketMiddleware.GetAllRoutes()
            .Keys.Single(endpoint => endpoint is DeviceWsEndPoint);

    private static System.Collections.Concurrent.ConcurrentDictionary<WebSocketConnection, byte> Connections()
    {
        return Endpoint().connections;
    }

    private static (DeviceWsEndPoint Endpoint,
        WebSocketConnection Connection,
        CapturingWebSocket Socket) CreateConnection()
    {
        var endpoint = Endpoint();
        var context = new DefaultHttpContext();
        context.RequestServices =
            new ServiceCollection().BuildServiceProvider();
        context.Features.Set<ISessionFeature>(
            new TestSessionFeature(new TestSession()));
        var socket = new CapturingWebSocket();
        var connection = new WebSocketConnection(
            context,
            socket,
            endpoint);
        return (endpoint, connection, socket);
    }

    public sealed class DeviceWsEndPoint : WsEndPoint
    {
        public override string DefinePath() => "/device-ws";
    }

    [EndPoint<DeviceWsEndPoint>]
    public sealed class DeviceWsRouter : StorableWsRouter<Device>
    {
        protected override IGenericDM<Device>? GetDM() =>
            (IGenericDM<Device>)GenericDM.Get<Device>();

        protected override string StorableName() => "ws-device";
    }

    private sealed class TestSessionFeature(ISession session)
        : ISessionFeature
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

    private sealed class CapturingWebSocket
        : System.Net.WebSockets.WebSocket
    {
        private TaskCompletionSource messageReceived = NewMessageSource();
        public List<string> Messages { get; } = [];
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort()
        {
        }
        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose()
        {
        }
        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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
            messageReceived.TrySetResult();
            return Task.CompletedTask;
        }

        public void ClearMessages()
        {
            Messages.Clear();
            messageReceived = NewMessageSource();
        }

        public Task WaitForMessage() =>
            messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        private static TaskCompletionSource NewMessageSource() => new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
