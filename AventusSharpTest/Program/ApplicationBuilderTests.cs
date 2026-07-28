using System.Text;
using System.Reflection;
using AventusSharp;
using AventusSharp.Routes;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Response;
using AventusSharp.SSE;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using HttpPath = AventusSharp.Routes.Attributes.Path;

namespace AventusSharpTest.Program;

[TestFixture]
[NonParallelizable]
public sealed class ApplicationBuilderTests
{
    private TestApplicationLifetime lifetime = null!;
    private IServiceProvider services = null!;

    [OneTimeSetUp]
    public void RegisterTestEndpoints()
    {
        var http = AventusSharp.Routes.RouterMiddleware.Register(
            [typeof(ApplicationRouter)]);
        Assert.That(
            http.Success ||
            http.Errors.All(error => error.Message.Contains("already", StringComparison.OrdinalIgnoreCase)),
            Is.True,
            string.Join(Environment.NewLine, http.Errors.Select(error => error.Message)));

        var sse = SSEMiddleware.Register([typeof(ApplicationSseEndPoint)]);
        Assert.That(sse.Success, Is.True,
            string.Join(Environment.NewLine, sse.Errors.Select(error => error.Message)));
    }

    [OneTimeTearDown]
    public void UnregisterTestEndpoint()
    {
        SSEMiddleware.endPointInstances.Remove("/application-builder-sse");
        var mainField = typeof(SSEMiddleware).GetField(
            "mainEndPoint",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (mainField?.GetValue(null) is ApplicationSseEndPoint)
        {
            mainField.SetValue(null, null);
        }
    }

    [SetUp]
    public void SetUp()
    {
        lifetime = new TestApplicationLifetime();
        services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostApplicationLifetime>(lifetime)
            .BuildServiceProvider();
    }

    [TearDown]
    public async Task TearDown()
    {
        await SSEMiddleware.Stop();
        lifetime.Dispose();
        if (services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Test]
    public async Task Use_aventus_http_adds_routing_to_a_real_application_pipeline()
    {
        var app = new ApplicationBuilder(services);
        var returned = app.UseAventusHttp();
        app.Run(async context =>
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("fallback");
        });
        var pipeline = app.Build();
        var routed = Context("GET", "/application-builder/hello");
        var unknown = Context("GET", "/application-builder/unknown");

        await pipeline(routed);
        await pipeline(unknown);

        Assert.Multiple(() =>
        {
            Assert.That(returned, Is.SameAs(app));
            Assert.That(ReadBody(routed), Is.EqualTo("application route"));
            Assert.That(routed.Response.StatusCode, Is.EqualTo(200));
            Assert.That(ReadBody(unknown), Is.EqualTo("fallback"));
            Assert.That(unknown.Response.StatusCode, Is.EqualTo(404));
        });
    }

    [Test]
    public async Task Use_aventus_sse_adds_endpoint_routing_to_the_pipeline()
    {
        var app = new ApplicationBuilder(services);
        app.UseAventusSSE();
        app.Run(context =>
        {
            context.Response.StatusCode = 404;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        using var cancellation = new CancellationTokenSource();
        var context = Context("GET", "/application-builder-sse");
        context.RequestAborted = cancellation.Token;

        var request = pipeline(context);
        Assert.That(
            SpinWait.SpinUntil(
                () => context.Response.Headers.ContentType.Count > 0,
                TimeSpan.FromSeconds(2)),
            Is.True);
        cancellation.Cancel();
        await request;

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
            Assert.That(context.Response.ContentType, Is.EqualTo("text/event-stream"));
            Assert.That(ReadBody(context), Does.Contain(": connected"));
        });
    }

    [Test]
    public void Application_stopping_closes_connections_registered_by_sse_extension()
    {
        var endpoint = (ApplicationSseEndPoint)
            SSEMiddleware.endPointInstances["/application-builder-sse"];
        var connection = new SSEConnection(
            Context("GET", "/application-builder-sse"),
            endpoint);
        endpoint.TrackConnection(connection);
        var app = new ApplicationBuilder(services);
        app.UseAventusSSE();

        lifetime.StopApplication();

        Assert.That(connection.WaitForShutdown.IsCompleted, Is.True);
    }

    [Test]
    public async Task Use_aventus_websocket_preserves_fallback_for_a_regular_request()
    {
        var app = new ApplicationBuilder(services);
        var returned = app.UseAventusWebsocket();
        app.Run(context => context.Response.WriteAsync("fallback"));
        var pipeline = app.Build();
        var context = Context("GET", "/ordinary-http-request");

        await pipeline(context);

        Assert.Multiple(() =>
        {
            Assert.That(returned, Is.SameAs(app));
            Assert.That(ReadBody(context), Is.EqualTo("fallback"));
        });
    }

    private DefaultHttpContext Context(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.RequestServices = services;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context) =>
        Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    [Prefix("/application-builder")]
    public sealed class ApplicationRouter : Router
    {
        [HttpPath("/hello")]
        [Get]
        public TextResponse Hello()
        {
            return new TextResponse("application route");
        }
    }

    public sealed class ApplicationSseEndPoint : SSEEndPoint
    {
        public override string DefinePath() => "/application-builder-sse";
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => started.Token;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => stopped.Token;

        public void StopApplication()
        {
            stopping.Cancel();
        }

        public void Dispose()
        {
            started.Dispose();
            stopping.Dispose();
            stopped.Dispose();
        }
    }
}
