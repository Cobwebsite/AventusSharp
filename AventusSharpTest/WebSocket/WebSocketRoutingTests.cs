using AventusSharp.WebSocket;
using AventusSharp.WebSocket.Attributes;
using AventusSharp.WebSocket.Event;
using NUnit.Framework;
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
            new[] { typeof(TestEndPoint) },
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
        Assert.That(endpoint.Value, Has.Count.EqualTo(2));
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

    public sealed class TestEndPoint : WsEndPoint
    {
        public override string DefinePath() => "/tests-ws";
        public override bool Main() => true;
    }

    [EndPoint<TestEndPoint>]
    public sealed class TestWsRouter : WsRouter
    {
        [WsPath("/devices/{id}")]
        public WebSocketEvent GetDevice(int id) => new JsonEvent(new { Id = id });

        [WsPath("/ping")]
        public WebSocketEvent Ping() => new EmptyEvent();
    }
}
