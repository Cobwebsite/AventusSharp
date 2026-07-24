using System.Text;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Response;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RoutePath = AventusSharp.Routes.Attributes.Path;

namespace AventusSharpTest.Routes;

[TestFixture]
public class ResponseTests
{
    [Test]
    public async Task Text_response_sets_status_content_type_and_body()
    {
        var context = CreateContext();

        await new TextResponse("hello", 201).send(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(201));
            Assert.That(context.Response.ContentType, Does.StartWith("text/plain"));
            Assert.That(ReadBody(context), Is.EqualTo("hello"));
        });
    }

    [Test]
    public async Task Json_response_serializes_an_object()
    {
        var context = CreateContext();

        await new Json(new { Name = "Aventus", Stable = true }).send(context);

        Assert.That(context.Response.ContentType, Does.StartWith("application/json"));
        var body = JObject.Parse(ReadBody(context));
        Assert.Multiple(() =>
        {
            Assert.That(body["Name"]?.Value<string>(), Is.EqualTo("Aventus"));
            Assert.That(body["Stable"]?.Value<bool>(), Is.True);
            Assert.That(body["$type"], Is.Not.Null,
                "The default Aventus JSON settings preserve runtime type metadata.");
        });
    }

    [Test]
    public async Task Byte_response_preserves_payload_and_content_type()
    {
        var context = CreateContext();
        var bytes = new byte[] { 1, 2, 3 };

        await new ByteResponse(bytes, "application/test", 202).send(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(202));
        Assert.That(context.Response.ContentType, Does.StartWith("application/test"));
        Assert.That(((MemoryStream)context.Response.Body).ToArray(), Is.EqualTo(bytes));
    }

    [TestCase("api", "/api")]
    [TestCase("/api/", "/api")]
    [TestCase("/", "/")]
    public void Prefix_normalizes_slashes(string input, string expected)
    {
        Assert.That(new Prefix(input).txt, Is.EqualTo(expected));
    }

    [TestCase("items", "/items")]
    [TestCase("/items/", "/items")]
    public void Path_normalizes_slashes(string input, string expected)
    {
        Assert.That(new RoutePath(input).pattern, Is.EqualTo(expected));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        return Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
    }
}
