using System.Reflection;
using AventusSharp.Routes;
using AventusSharp.SSE;
using AventusSharp.Tools;
using AventusSharp.WebSocket;
using NUnit.Framework;

namespace AventusSharpTest.Program;

[TestFixture]
[NonParallelizable]
public sealed class MiddlewareConfigurationLifecycleTests
{
    private Dictionary<string, SSEEndPoint> originalSseEndpoints = null!;
    private object? originalMainSseEndpoint;

    [SetUp]
    public void CaptureSseState()
    {
        originalSseEndpoints = SSEMiddleware.endPointInstances
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        originalMainSseEndpoint = MainSseField().GetValue(null);
    }

    [TearDown]
    public void ResetConfigurationActions()
    {
        RouterMiddleware.Configure(_ => { });
        SSEMiddleware.Configure(_ => { });
        WebSocketMiddleware.Configure(_ => { });
        SetConfigLoaded(typeof(RouterMiddleware), false);
        SetConfigLoaded(typeof(SSEMiddleware), false);
        SetConfigLoaded(typeof(WebSocketMiddleware), false);
        SSEMiddleware.endPointInstances.Clear();
        foreach (var endpoint in originalSseEndpoints)
        {
            SSEMiddleware.endPointInstances.Add(endpoint.Key, endpoint.Value);
        }
        MainSseField().SetValue(null, originalMainSseEndpoint);
    }

    [Test]
    public void Http_configuration_is_applied_only_once()
    {
        var calls = 0;
        SetConfigLoaded(typeof(RouterMiddleware), false);
        RouterMiddleware.Configure(_ => calls++);

        var first = RouterMiddleware.Register(Array.Empty<Type>());
        var second = RouterMiddleware.Register(Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Sse_configuration_is_applied_only_once()
    {
        var calls = 0;
        SetConfigLoaded(typeof(SSEMiddleware), false);
        SSEMiddleware.Configure(_ => calls++);

        var first = SSEMiddleware.Register(Array.Empty<Type>());
        var second = SSEMiddleware.Register(Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Websocket_configuration_is_applied_only_once()
    {
        var calls = 0;
        SetConfigLoaded(typeof(WebSocketMiddleware), false);
        WebSocketMiddleware.Configure(_ => calls++);

        var first = WebSocketMiddleware.Register(
            Array.Empty<Type>(),
            Array.Empty<Type>());
        var second = WebSocketMiddleware.Register(
            Array.Empty<Type>(),
            Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Failed_configuration_is_reported_and_can_be_retried()
    {
        var calls = 0;
        SetConfigLoaded(typeof(SSEMiddleware), false);
        SSEMiddleware.Configure(_ =>
        {
            calls++;
            if (calls == 1)
            {
                throw new InvalidOperationException("configuration failure");
            }
        });

        var failed = SSEMiddleware.Register(Array.Empty<Type>());
        var retried = SSEMiddleware.Register(Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(failed.Success, Is.False);
            Assert.That(failed.Errors.Single().Message,
                Does.Contain("configuration failure"));
            Assert.That(retried.Success, Is.True);
            Assert.That(calls, Is.EqualTo(2));
        });
    }

    [Test]
    public void Failed_http_configuration_is_returned_as_a_route_error_and_can_be_retried()
    {
        var calls = 0;
        SetConfigLoaded(typeof(RouterMiddleware), false);
        RouterMiddleware.Configure(_ =>
        {
            calls++;
            if (calls == 1)
            {
                throw new InvalidOperationException("http configuration failure");
            }
        });

        VoidWithError? failed = null;
        Assert.DoesNotThrow(() =>
            failed = RouterMiddleware.Register(Array.Empty<Type>()));
        var retried = RouterMiddleware.Register(Array.Empty<Type>());

        Assert.Multiple(() =>
        {
            Assert.That(failed?.Success, Is.False);
            Assert.That(failed?.Errors.OfType<RouteError>()
                    .Select(error => error.Code),
                Does.Contain(RouteErrorCode.ConfigError));
            Assert.That(failed?.Errors.Single().Message,
                Does.Contain("http configuration failure"));
            Assert.That(retried.Success, Is.True);
            Assert.That(calls, Is.EqualTo(2));
        });
    }

    private static void SetConfigLoaded(Type middleware, bool value)
    {
        middleware.GetField(
            "configLoaded",
            BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, value);
    }

    private static FieldInfo MainSseField()
    {
        return typeof(SSEMiddleware).GetField(
            "mainEndPoint",
            BindingFlags.Static | BindingFlags.NonPublic)!;
    }
}
