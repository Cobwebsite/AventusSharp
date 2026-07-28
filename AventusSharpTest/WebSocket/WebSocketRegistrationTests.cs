using System.Reflection;
using AventusSharp.WebSocket;
using NUnit.Framework;

namespace AventusSharpTest.WebSocket;

[TestFixture]
[NonParallelizable]
public sealed class WebSocketRegistrationTests
{
    private Dictionary<string, WsEndPoint> originalEndpoints = null!;
    private object? originalMainEndpoint;

    [SetUp]
    public void SetUp()
    {
        originalEndpoints = WebSocketMiddleware.endPointInstances
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        originalMainEndpoint = MainField().GetValue(null);
        WebSocketMiddleware.endPointInstances.Clear();
        MainField().SetValue(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        WebSocketMiddleware.endPointInstances.Clear();
        foreach (var endpoint in originalEndpoints)
        {
            WebSocketMiddleware.endPointInstances.Add(endpoint.Key, endpoint.Value);
        }
        MainField().SetValue(null, originalMainEndpoint);
    }

    [Test]
    public void Abstract_endpoints_are_ignored_and_default_is_created_lazily()
    {
        var result = WebSocketMiddleware.Register(
            [typeof(AbstractEndpoint)],
            Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(WebSocketMiddleware.endPointInstances, Is.Empty);
        });

        var main = WebSocketMiddleware.GetMain();

        Assert.Multiple(() =>
        {
            Assert.That(main, Is.TypeOf<DefaultWsEndPoint>());
            Assert.That(WebSocketMiddleware.endPointInstances["/ws"],
                Is.SameAs(main));
        });
    }

    [Test]
    public void Duplicate_paths_keep_the_first_registered_endpoint()
    {
        var result = WebSocketMiddleware.Register(
            [typeof(FirstDuplicateEndpoint), typeof(SecondDuplicateEndpoint)],
            Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(WebSocketMiddleware.endPointInstances, Has.Count.EqualTo(1));
            Assert.That(WebSocketMiddleware.endPointInstances["/duplicate-ws"],
                Is.TypeOf<FirstDuplicateEndpoint>());
        });
    }

    [Test]
    public void Single_registered_endpoint_becomes_main_automatically()
    {
        var result = WebSocketMiddleware.Register(
            [typeof(RegularEndpoint)],
            Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(WebSocketMiddleware.GetMain(), Is.TypeOf<RegularEndpoint>());
        });
    }

    [Test]
    public void Explicit_main_endpoint_is_selected_among_multiple_endpoints()
    {
        var result = WebSocketMiddleware.Register(
            [typeof(RegularEndpoint), typeof(MainEndpoint)],
            Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(WebSocketMiddleware.GetMain(), Is.TypeOf<MainEndpoint>());
            Assert.That(WebSocketMiddleware.endPointInstances.Keys,
                Is.EquivalentTo(new[] { "/regular-ws", "/main-ws" }));
        });
    }

    [Test]
    public void Multiple_explicit_main_endpoints_are_reported()
    {
        var result = WebSocketMiddleware.Register(
            [typeof(MainEndpoint), typeof(OtherMainEndpoint)],
            Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors.OfType<WsError>()
                    .Select(error => error.Code),
                Does.Contain(WsErrorCode.MultipleMainEndpoint));
            Assert.That(WebSocketMiddleware.GetMain(), Is.TypeOf<MainEndpoint>());
            Assert.That(WebSocketMiddleware.endPointInstances, Has.Count.EqualTo(2));
        });
    }

    private static FieldInfo MainField()
    {
        return typeof(WebSocketMiddleware).GetField(
            "mainEndPoint",
            BindingFlags.Static | BindingFlags.NonPublic)!;
    }

    public abstract class AbstractEndpoint : WsEndPoint
    {
    }

    public sealed class FirstDuplicateEndpoint : WsEndPoint
    {
        public override string DefinePath() => "/duplicate-ws";
    }

    public sealed class SecondDuplicateEndpoint : WsEndPoint
    {
        public override string DefinePath() => "/duplicate-ws";
    }

    public sealed class RegularEndpoint : WsEndPoint
    {
        public override string DefinePath() => "/regular-ws";
    }

    public sealed class MainEndpoint : WsEndPoint
    {
        public override string DefinePath() => "/main-ws";
        public override bool Main() => true;
    }

    public sealed class OtherMainEndpoint : WsEndPoint
    {
        public override string DefinePath() => "/other-main-ws";
        public override bool Main() => true;
    }
}
