using System.Text;
using AventusSharp.Hosting;
using AventusSharp.Maui;
using NUnit.Framework;

namespace AventusSharpTest.Hosting;

public sealed class AventusMauiBridgeTests
{
    [Test]
    public async Task Bridge_maps_a_WebView_request_and_response_without_AspNetCore()
    {
        var dispatcher = new CapturingDispatcher();
        var bridge = new AventusMauiBridge(dispatcher, () => new EmptyServiceProvider());

        AventusBridgeResponse response = await bridge.ExecuteAsync(
            new AventusBridgeRequest(
                "POST",
                "https://0.0.0.1/api/items?page=2",
                Encoding.UTF8.GetBytes("request"),
                "application/json",
                new Dictionary<string, string[]> { ["X-Test"] = ["value"] }));

        Assert.Multiple(() =>
        {
            Assert.That(dispatcher.Context?.Request.Method, Is.EqualTo("POST"));
            Assert.That(dispatcher.Context?.Request.Path, Is.EqualTo("/api/items"));
            Assert.That(dispatcher.Context?.Request.QueryString, Is.EqualTo("?page=2"));
            Assert.That(dispatcher.Context?.Request.Headers["X-Test"], Is.EqualTo(new[] { "value" }));
            Assert.That(response.Status, Is.EqualTo(201));
            Assert.That(Encoding.UTF8.GetString(response.Content), Is.EqualTo("response"));
            Assert.That(response.Headers["Content-Type"], Is.EqualTo(new[] { "text/plain" }));
        });
    }

    private sealed class CapturingDispatcher : IAventusRequestDispatcher
    {
        public IAventusContext? Context { get; private set; }

        public async Task DispatchAsync(IAventusContext context)
        {
            Context = context;
            context.Response.StatusCode = 201;
            context.Response.ContentType = "text/plain";
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("response"));
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
