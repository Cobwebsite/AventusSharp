using AventusSharp.AspNetCore.Hosting;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace AventusSharpTest.Hosting;

public sealed class AspNetCoreAventusContextTests
{
    [Test]
    public void Adapter_reads_and_writes_the_native_context()
    {
        var native = new DefaultHttpContext();
        native.RequestServices = new ServiceCollection().BuildServiceProvider();
        native.Request.Method = "POST";
        native.Request.Path = "/api/test";
        native.Request.QueryString = new QueryString("?page=2");
        native.Request.Headers["X-Request"] = "value";

        var context = native.GetAventusContext();

        Assert.That(context.Request.Method, Is.EqualTo("POST"));
        Assert.That(context.Request.Path, Is.EqualTo("/api/test"));
        Assert.That(context.Request.QueryString, Is.EqualTo("?page=2"));
        Assert.That(context.Request.Headers["X-Request"], Is.EqualTo(new[] { "value" }));

        context.Response.StatusCode = 201;
        context.Response.ContentType = "application/json";
        context.Response.Headers["X-Response"] = ["one", "two"];

        Assert.That(native.Response.StatusCode, Is.EqualTo(201));
        Assert.That(native.Response.ContentType, Is.EqualTo("application/json"));
        Assert.That(native.Response.Headers["X-Response"].ToArray(), Is.EqualTo(new[] { "one", "two" }));
    }
}
