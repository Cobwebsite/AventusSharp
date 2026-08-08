using System.Text;
using AventusSharp.Routes;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Response;
using Microsoft.AspNetCore.Http;
using AventusSharp.AspNetCore.Hosting;
using AventusSharp.Hosting;
using AventusSharp.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using IOPath = System.IO.Path;
using RoutePath = AventusSharp.Routes.Attributes.Path;

namespace AventusSharpTest.Routes;

[TestFixture]
[NonParallelizable]
public class ResponseTests
{
    private Func<IAventusContext, IRouter?, string>? originalViewDir;
    private bool originalMapErrorCodeToHttpStatusCode;

    [SetUp]
    public void SaveViewConfiguration()
    {
        originalViewDir = RouterMiddleware.config.ViewDir;
        originalMapErrorCodeToHttpStatusCode =
            RouterMiddleware.config.MapErrorCodeToHttpStatusCode;
    }

    [TearDown]
    public void RestoreViewConfiguration()
    {
        if (originalViewDir is not null)
        {
            RouterMiddleware.config.ViewDir = originalViewDir;
        }
        RouterMiddleware.config.MapErrorCodeToHttpStatusCode =
            originalMapErrorCodeToHttpStatusCode;
    }

    [Test]
    public void Concrete_route_responses_keep_the_IResponse_contract()
    {
        Type[] expectedTypes =
        [
            typeof(ByteResponse),
            typeof(DummyResponse),
            typeof(Json),
            typeof(NoResponse),
            typeof(Redirect),
            typeof(StreamResponse),
            typeof(TextResponse),
            typeof(View),
            typeof(ViewDynamic)
        ];

        Assert.That(
            expectedTypes,
            Is.All.Matches<Type>(type => typeof(IResponse).IsAssignableFrom(type)));
        Assert.That(
            typeof(IResponse).GetMethod(nameof(IResponse.send))?.ReturnType,
            Is.EqualTo(typeof(Task)));
    }

    [Test]
    public async Task Text_response_sets_status_content_type_and_body()
    {
        var context = CreateContext();

        await new TextResponse("hello", 201).send(Adapt(context));

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

        await new Json(new { Name = "Aventus", Stable = true }).send(Adapt(context));

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
    public void Error_code_to_http_status_mapping_is_enabled_by_default()
    {
        Assert.That(new RouterConfig().MapErrorCodeToHttpStatusCode, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Json_response_maps_the_first_error_code_to_http_status(
        bool hasResult)
    {
        RouterMiddleware.config.MapErrorCodeToHttpStatusCode = true;
        IWithError error;
        if (hasResult)
        {
            var result = new ResultWithError<string>();
            result.Errors.Add(new GenericError(404, "Missing resource"));
            error = result;
        }
        else
        {
            var result = new VoidWithError();
            result.Errors.Add(new GenericError(404, "Missing resource"));
            error = result;
        }
        var context = CreateContext();

        await new Json(error).send(Adapt(context));

        Assert.That(context.Response.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task Json_response_keeps_success_status_when_error_mapping_is_disabled()
    {
        RouterMiddleware.config.MapErrorCodeToHttpStatusCode = false;
        var error = new VoidWithError();
        error.Errors.Add(new GenericError(404, "Missing resource"));
        var context = CreateContext();

        await new Json(error).send(Adapt(context));

        Assert.That(context.Response.StatusCode, Is.EqualTo(200));
    }

    [Test]
    public async Task Json_response_keeps_an_explicit_status_code()
    {
        RouterMiddleware.config.MapErrorCodeToHttpStatusCode = true;
        var error = new VoidWithError();
        error.Errors.Add(new GenericError(404, "Missing resource"));
        var context = CreateContext();

        await new Json(error, 409).send(Adapt(context));

        Assert.That(context.Response.StatusCode, Is.EqualTo(409));
    }

    [Test]
    public async Task Byte_response_preserves_payload_and_content_type()
    {
        var context = CreateContext();
        var bytes = new byte[] { 1, 2, 3 };

        await new ByteResponse(bytes, "application/test", 202).send(Adapt(context));

        Assert.That(context.Response.StatusCode, Is.EqualTo(202));
        Assert.That(context.Response.ContentType, Does.StartWith("application/test"));
        Assert.That(((MemoryStream)context.Response.Body).ToArray(), Is.EqualTo(bytes));
    }

    [Test]
    public async Task Redirect_sets_redirect_status_and_location()
    {
        var context = CreateContext();

        await new Redirect("/target").send(Adapt(context));

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(302));
            Assert.That(context.Response.Headers.Location.ToString(),
                Is.EqualTo("/target"));
        });
    }

    [Test]
    public async Task Stream_response_copies_payload_and_disposes_source()
    {
        var source = new TrackingMemoryStream(
            Encoding.UTF8.GetBytes("streamed"));
        var context = CreateContext();

        await new StreamResponse(source, "application/custom", 206)
            .send(Adapt(context));

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(206));
            Assert.That(context.Response.ContentType,
                Does.StartWith("application/custom"));
            Assert.That(ReadBody(context), Is.EqualTo("streamed"));
            Assert.That(source.IsDisposed, Is.True);
        });
    }

    [Test]
    public async Task No_response_preserves_the_existing_response()
    {
        var context = CreateContext();
        context.Response.StatusCode = 202;
        await context.Response.WriteAsync("already written");

        await new NoResponse().send(Adapt(context));

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(202));
            Assert.That(ReadBody(context), Is.EqualTo("already written"));
        });
    }

    [Test]
    public async Task Missing_view_returns_a_descriptive_bad_request()
    {
        var context = CreateContext();
        var viewName = $"missing-{Guid.NewGuid():N}";

        await new View(viewName).send(Adapt(context));

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(400));
            Assert.That(ReadBody(context), Does.Contain(viewName));
            Assert.That(context.Response.ContentLength,
                Is.EqualTo(((MemoryStream)context.Response.Body).Length));
        });
    }

    [Test]
    public async Task View_returns_the_existing_html_file()
    {
        string directory = CreateViewDirectory();
        await File.WriteAllTextAsync(
            IOPath.Combine(directory, "plain.html"),
            "<h1>Aventus</h1>");
        RouterMiddleware.config.ViewDir = (_, _) => directory;
        var context = CreateContext();

        await new View("plain").send(Adapt(context));

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
            Assert.That(context.Response.ContentType, Does.StartWith("text/html"));
            Assert.That(ReadBody(context), Is.EqualTo("<h1>Aventus</h1>"));
            Assert.That(context.Response.ContentLength, Is.EqualTo(16));
        });
    }

    [Test]
    public async Task Dynamic_view_renders_the_model()
    {
        string directory = CreateViewDirectory();
        await File.WriteAllTextAsync(
            IOPath.Combine(directory, "device.sbnhtml"),
            "<p>{{ name }}: {{ value }}</p>");
        RouterMiddleware.config.ViewDir = (_, _) => directory;
        var context = CreateContext();

        await new ViewDynamic("device", new { Name = "Light", Value = 42 })
            .send(Adapt(context), null);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
            Assert.That(context.Response.ContentType, Does.StartWith("text/html"));
            Assert.That(ReadBody(context), Is.EqualTo("<p>Light: 42</p>"));
        });
    }

    [Test]
    public async Task Dynamic_view_cache_is_isolated_by_full_path()
    {
        string firstDirectory = CreateViewDirectory();
        string secondDirectory = CreateViewDirectory();
        const string viewName = "shared-name";
        await File.WriteAllTextAsync(
            IOPath.Combine(firstDirectory, viewName + ".sbnhtml"),
            "first {{ name }}");
        await File.WriteAllTextAsync(
            IOPath.Combine(secondDirectory, viewName + ".sbnhtml"),
            "second {{ name }}");

        RouterMiddleware.config.ViewDir = (_, _) => firstDirectory;
        var firstContext = CreateContext();
        await new ViewDynamic(viewName, new { Name = "A" })
            .send(Adapt(firstContext), null);

        RouterMiddleware.config.ViewDir = (_, _) => secondDirectory;
        var secondContext = CreateContext();
        await new ViewDynamic(viewName, new { Name = "B" })
            .send(Adapt(secondContext), null);

        Assert.Multiple(() =>
        {
            Assert.That(ReadBody(firstContext), Is.EqualTo("first A"));
            Assert.That(ReadBody(secondContext), Is.EqualTo("second B"));
        });
    }

    [Test]
    public async Task Missing_dynamic_view_returns_a_descriptive_bad_request()
    {
        string directory = CreateViewDirectory();
        RouterMiddleware.config.ViewDir = (_, _) => directory;
        var context = CreateContext();

        await new ViewDynamic("missing-template", new { Name = "Unused" })
            .send(Adapt(context), null);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(400));
            Assert.That(ReadBody(context), Does.Contain("missing-template.sbnhtml"));
            Assert.That(context.Response.ContentLength,
                Is.EqualTo(((MemoryStream)context.Response.Body).Length));
        });
    }

    [Test]
    public async Task Dynamic_view_supports_concurrent_first_render()
    {
        string directory = CreateViewDirectory();
        const string viewName = "concurrent";
        await File.WriteAllTextAsync(
            IOPath.Combine(directory, viewName + ".sbnhtml"),
            "{{ name }}-{{ value }}");
        RouterMiddleware.config.ViewDir = (_, _) => directory;

        var renders = Enumerable.Range(0, 32)
            .Select(async index =>
            {
                var context = CreateContext();
                await new ViewDynamic(
                        viewName,
                        new { Name = "item", Value = index })
                    .send(Adapt(context), null);
                return (context, body: ReadBody(context), index);
            })
            .ToArray();

        var results = await Task.WhenAll(renders);

        Assert.Multiple(() =>
        {
            Assert.That(results.Select(result => result.context.Response.StatusCode),
                Is.All.EqualTo(200));
            foreach (var result in results)
            {
                Assert.That(result.body, Is.EqualTo($"item-{result.index}"));
            }
        });
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

    private static AventusSharp.AspNetCore.Hosting.AventusContext Adapt(HttpContext context) => new(context);

    private static string ReadBody(HttpContext context)
    {
        return Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
    }

    private static string CreateViewDirectory()
    {
        string directory = IOPath.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "view-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TrackingMemoryStream(byte[] buffer)
        : MemoryStream(buffer)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
