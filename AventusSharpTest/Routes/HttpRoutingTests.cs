using System.Text;
using AventusSharp.Routes;
using AventusSharp.Routes.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using HttpPath = AventusSharp.Routes.Attributes.Path;

namespace AventusSharpTest.Routes;

[TestFixture]
[NonParallelizable]
public sealed class HttpRoutingTests
{
    [OneTimeSetUp]
    public void RegisterRoutes()
    {
        var result = RouterMiddleware.Register(new[] { typeof(TestRouter) });
        Assert.That(result.Success, Is.True,
            string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }

    [Test]
    public void Route_listing_contains_methods_paths_and_parameters()
    {
        var routes = RouterMiddleware.GetAllRoutes()
            .Where(route => route.router is TestRouter)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(routes, Has.Count.EqualTo(3));
            Assert.That(routes.Any(route =>
                route.baseUrl == "/tests/hello/{name}" &&
                route.method == AventusSharp.Routes.Request.MethodType.Get), Is.True);
            Assert.That(routes.Any(route =>
                route.baseUrl == "/tests/sum" &&
                route.method == AventusSharp.Routes.Request.MethodType.Post), Is.True);
        });
    }

    [Test]
    public async Task Get_route_binds_path_parameter_and_writes_response()
    {
        var context = CreateContext("GET", "/tests/hello/Aventus");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(context.Response.StatusCode, Is.EqualTo(200));
        Assert.That(ReadBody(context), Is.EqualTo("Hello Aventus"));
    }

    [Test]
    public async Task Post_route_binds_json_body_and_serializes_result()
    {
        var context = CreateContext("POST", "/tests/sum");
        var body = Encoding.UTF8.GetBytes("""{"body":{"left":4,"right":7}}""");
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(body);

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        var json = JObject.Parse(ReadBody(context));
        Assert.That(json["Result"]?.Value<int>(), Is.EqualTo(11));
    }

    [Test]
    public async Task Unknown_route_calls_next_middleware()
    {
        var context = CreateContext("GET", "/not-registered");
        var nextCalled = false;

        await RouterMiddleware.OnRequest(context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.That(nextCalled, Is.True);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context) =>
        Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    [Prefix("/tests")]
    public sealed class TestRouter : Router
    {
        [Get]
        [HttpPath("/hello/{name}")]
        public string Hello(string name) => $"Hello {name}";

        [Post]
        [HttpPath("/sum")]
        public object Sum(SumBody body) => new { Result = body.Left + body.Right };

        [Get]
        [HttpPath("/context")]
        public string Context(HttpContext context) => context.Request.Method;
    }

    public sealed class SumBody
    {
        public int Left { get; set; }
        public int Right { get; set; }
    }
}
