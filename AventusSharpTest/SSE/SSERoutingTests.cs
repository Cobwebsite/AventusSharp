using AventusSharp.SSE;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace AventusSharpTest.SSE;

[TestFixture]
[NonParallelizable]
public sealed class SSERoutingTests
{
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

        Assert.That(context.Response.ContentType, Does.StartWith("text/event-stream"));
    }

    public sealed class TestSseEndPoint : SSEEndPoint
    {
        public override string DefinePath() => "/tests-sse";
        public override bool Main() => true;
    }
}
