using System.Text;
using AventusSharp.Routes;
using AventusSharp.Routes.Attributes;
using AventusSharp.Routes.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using AventusSharp.Tools.Attributes;
using HttpPath = AventusSharp.Routes.Attributes.Path;

namespace AventusSharpTest.Routes;

[TestFixture]
[NonParallelizable]
public sealed class HttpRoutingTests
{
    [OneTimeSetUp]
    public void RegisterRoutes()
    {
        var result = RouterMiddleware.Register(
            new[] { typeof(TestRouter), typeof(RegexRouter) });
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
            Assert.That(routes, Has.Count.EqualTo(25));
            Assert.That(routes.Any(route =>
                route.baseUrl == "/tests/hello/{name}" &&
                route.method == AventusSharp.Routes.Request.MethodType.Get), Is.True);
            Assert.That(routes.Any(route =>
                route.baseUrl == "/tests/sum" &&
                route.method == AventusSharp.Routes.Request.MethodType.Post), Is.True);
            var number = routes.Single(route =>
                route.baseUrl == "/tests/number/{id}");
            Assert.That(number.parameters["id"].type, Is.EqualTo(typeof(int)));
            Assert.That(number.parameters["id"].positionUrl, Is.EqualTo(0));
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

    [Test]
    public async Task Routing_is_case_insensitive_and_converts_integer_path_parameter()
    {
        var context = CreateContext("GET", "/TESTS/NUMBER/42");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(ReadBody(context), Is.EqualTo("Number 42"));
    }

    [Test]
    public async Task Invalid_integer_path_and_wrong_http_method_call_next_middleware()
    {
        var invalidInteger = CreateContext("GET", "/tests/number/not-a-number");
        var wrongMethod = CreateContext("POST", "/tests/hello/Aventus");
        var invalidNext = false;
        var methodNext = false;

        await RouterMiddleware.OnRequest(invalidInteger, () =>
        {
            invalidNext = true;
            return Task.CompletedTask;
        });
        await RouterMiddleware.OnRequest(wrongMethod, () =>
        {
            methodNext = true;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(invalidNext, Is.True);
            Assert.That(methodNext, Is.True);
        });
    }

    [Test]
    public async Task HttpContext_and_context_scope_are_available_only_during_route_execution()
    {
        var context = CreateContext("GET", "/tests/scope");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(ReadBody(context), Is.EqualTo("GET:True"));
            Assert.That(RouterMiddleware.ContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Async_generic_task_is_awaited_and_serialized()
    {
        var context = CreateContext("GET", "/tests/async");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        var json = JObject.Parse(ReadBody(context));
        Assert.That(json["Value"]?.Value<int>(), Is.EqualTo(12));
    }

    [TestCase("/tests/no-content")]
    [TestCase("/tests/no-content-async")]
    public async Task Void_and_non_generic_task_return_no_content(string path)
    {
        var context = CreateContext("POST", path);

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(204));
            Assert.That(ReadBody(context), Is.Empty);
        });
    }

    [Test]
    public async Task Request_services_are_injected_without_parsing_a_body()
    {
        var services = new ServiceCollection()
            .AddSingleton(new RouteDependency("injected"))
            .BuildServiceProvider();
        var context = CreateContext("GET", "/tests/service", services);

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(ReadBody(context), Is.EqualTo("injected"));
    }

    [TestCase(null, """{"body":{"left":1,"right":2}}""")]
    [TestCase("text/plain", """{"body":{"left":1,"right":2}}""")]
    [TestCase("application/json", "{invalid")]
    [TestCase("application/json", "{}")]
    public async Task Invalid_or_incomplete_body_returns_422(
        string? contentType,
        string body)
    {
        var context = CreateContext("POST", "/tests/sum");
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(context.Response.StatusCode, Is.EqualTo(422));
        var json = JObject.Parse(ReadBody(context));
        Assert.That(json["Errors"], Is.Not.Null);
    }

    [Test]
    public async Task Middleware_can_continue_and_receives_the_resolved_route()
    {
        TrackingMiddlewareAttribute.Reset();
        var context = CreateContext("GET", "/tests/middleware");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(ReadBody(context), Is.EqualTo("after middleware"));
            Assert.That(TrackingMiddlewareAttribute.Routes,
                Is.EqualTo(new[] { "/tests/middleware" }));
            Assert.That(RouterMiddleware.ContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Middleware_can_short_circuit_the_route()
    {
        var context = CreateContext("GET", "/tests/blocked");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(403));
            Assert.That(ReadBody(context), Is.EqualTo("blocked"));
            Assert.That(RouterMiddleware.ContextScope, Is.Null);
        });
    }

    [TestCase("GET", "/tests/multi-a")]
    [TestCase("POST", "/tests/multi-a")]
    [TestCase("GET", "/tests/multi-b")]
    [TestCase("POST", "/tests/multi-b")]
    public async Task Multiple_paths_and_methods_register_every_combination(
        string method,
        string path)
    {
        var context = CreateContext(method, path);

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(ReadBody(context), Is.EqualTo(method));
    }

    [TestCase("/tests/throw-aventus", "expected route failure")]
    [TestCase("/tests/throw-unexpected", "unexpected route failure")]
    public async Task Route_exceptions_return_json_500_and_clear_context_scope(
        string path,
        string expectedMessage)
    {
        var context = CreateContext("GET", path);

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(context.Response.StatusCode, Is.EqualTo(500));
        var json = JObject.Parse(ReadBody(context));
        Assert.Multiple(() =>
        {
            Assert.That(json["Errors"]?[0]?["Message"]?.Value<string>(),
                Does.Contain(expectedMessage));
            Assert.That(RouterMiddleware.ContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Method_without_path_uses_prefix_and_lowercase_method_name()
    {
        var context = CreateContext("GET", "/tests/defaultroute");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(ReadBody(context), Is.EqualTo("default route"));
    }

    [Test]
    public void NoRoute_method_is_not_registered()
    {
        var routes = RouterMiddleware.GetAllRoutes()
            .Where(route => route.router is TestRouter)
            .ToList();

        Assert.That(routes.Any(route =>
            route.action.Name == nameof(TestRouter.HiddenRoute)), Is.False);
    }

    [Test]
    public async Task Function_placeholder_is_resolved_when_route_is_registered()
    {
        var context = CreateContext("GET", "/tests/dynamic/segment");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(ReadBody(context), Is.EqualTo("dynamic route"));
    }

    [Test]
    public void Duplicate_route_is_reported_and_original_route_remains_registered()
    {
        var first = RouterMiddleware.Register([typeof(DuplicateRouterOne)]);
        var second = RouterMiddleware.Register([typeof(DuplicateRouterTwo)]);
        var duplicates = RouterMiddleware.GetAllRoutes()
            .Where(route => route.baseUrl == "/duplicate")
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(first.Success, Is.True,
                string.Join(Environment.NewLine,
                    first.Errors.Select(error => error.Message)));
            Assert.That(second.Success, Is.False);
            Assert.That(second.Errors.OfType<RouteError>()
                    .Select(error => error.Code),
                Does.Contain(RouteErrorCode.RouteAlreadyExist));
            Assert.That(duplicates, Has.Count.EqualTo(1));
            Assert.That(duplicates[0].router, Is.TypeOf<DuplicateRouterOne>());
        });
    }

    [Test]
    public async Task Multipart_upload_binds_file_and_removes_test_temp_file()
    {
        var fileName = $"aventus-http-{Guid.NewGuid():N}.txt";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("uploaded content")),
            "file",
            fileName);
        await using var requestBody = new MemoryStream();
        await multipart.CopyToAsync(requestBody);
        requestBody.Position = 0;
        var context = CreateContext("POST", "/tests/upload");
        context.Request.ContentType = multipart.Headers.ContentType!.ToString();
        context.Request.Body = requestBody;

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        var json = JObject.Parse(ReadBody(context));
        var filePath = json["FilePath"]?.Value<string>();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.Response.StatusCode, Is.EqualTo(200));
                Assert.That(json["FileName"]?.Value<string>(),
                    Is.EqualTo(fileName));
                Assert.That(json["Content"]?.Value<string>(),
                    Is.EqualTo("uploaded content"));
                Assert.That(json["IsInsideTemp"]?.Value<bool>(), Is.True);
                Assert.That(filePath, Is.Not.Null);
                Assert.That(File.Exists(filePath), Is.True);
            });
        }
        finally
        {
            if (filePath != null && File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Test]
    public async Task Concurrent_requests_keep_context_scope_isolated()
    {
        TestRouter.ResetConcurrentScope();
        var first = CreateContext("GET", "/tests/concurrent-scope/first");
        var second = CreateContext("GET", "/tests/concurrent-scope/second");

        await Task.WhenAll(
            RouterMiddleware.OnRequest(first, () => Task.CompletedTask),
            RouterMiddleware.OnRequest(second, () => Task.CompletedTask));

        var firstJson = JObject.Parse(ReadBody(first));
        var secondJson = JObject.Parse(ReadBody(second));
        Assert.Multiple(() =>
        {
            Assert.That(firstJson["Id"]?.Value<string>(), Is.EqualTo("first"));
            Assert.That(firstJson["Scope"]?.Value<string>(),
                Is.EqualTo("/tests/concurrent-scope/first"));
            Assert.That(secondJson["Id"]?.Value<string>(), Is.EqualTo("second"));
            Assert.That(secondJson["Scope"]?.Value<string>(),
                Is.EqualTo("/tests/concurrent-scope/second"));
            Assert.That(RouterMiddleware.ContextScope, Is.Null);
        });
    }

    [Test]
    public async Task Missing_optional_body_parameter_does_not_return_422()
    {
        var context = CreateContext("POST", "/tests/optional");
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(
            Encoding.UTF8.GetBytes("{}"));

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
            Assert.That(ReadBody(context), Is.EqualTo("<null>"));
        });
    }

    [TestCase("/regex/AB12", true)]
    [TestCase("/regex/ab12", false)]
    [TestCase("/regex/ABC12", false)]
    [TestCase("/regex/AB123", false)]
    public async Task PathRegex_matches_the_complete_configured_pattern(
        string path,
        bool expectedMatch)
    {
        var context = CreateContext("GET", path);
        var nextCalled = false;

        await RouterMiddleware.OnRequest(context, () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.EqualTo(!expectedMatch));
            Assert.That(ReadBody(context),
                Is.EqualTo(expectedMatch ? "regex" : ""));
        });
    }

    [Test]
    public async Task Multipart_upload_binds_a_list_of_files()
    {
        var firstName = $"aventus-list-a-{Guid.NewGuid():N}.txt";
        var secondName = $"aventus-list-b-{Guid.NewGuid():N}.txt";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("first file")),
            "files[0]",
            firstName);
        multipart.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("second file")),
            "files[1]",
            secondName);
        var context = await CreateMultipartContext("/tests/upload-many", multipart);

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        var json = JArray.Parse(ReadBody(context));
        var paths = json.Select(item => item["FilePath"]?.Value<string>())
            .Where(path => path != null)
            .Cast<string>()
            .ToList();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.Response.StatusCode, Is.EqualTo(200));
                Assert.That(json.Select(item => item["FileName"]?.Value<string>()),
                    Is.EqualTo(new[] { firstName, secondName }));
                Assert.That(json.Select(item => item["Content"]?.Value<string>()),
                    Is.EqualTo(new[] { "first file", "second file" }));
            });
        }
        finally
        {
            foreach (var path in paths.Where(File.Exists))
                File.Delete(path);
            context.Request.Body.Dispose();
        }
    }

    [Test]
    public async Task Multipart_upload_binds_fields_and_file_inside_an_object()
    {
        var fileName = $"aventus-nested-{Guid.NewGuid():N}.txt";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent("nested name"), "payload[name]");
        multipart.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("nested file")),
            "payload[file]",
            fileName);
        var context = await CreateMultipartContext("/tests/upload-nested", multipart);

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        var json = JObject.Parse(ReadBody(context));
        var filePath = json["FilePath"]?.Value<string>();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.Response.StatusCode, Is.EqualTo(200));
                Assert.That(json["Name"]?.Value<string>(),
                    Is.EqualTo("nested name"));
                Assert.That(json["FileName"]?.Value<string>(),
                    Is.EqualTo(fileName));
                Assert.That(json["Content"]?.Value<string>(),
                    Is.EqualTo("nested file"));
            });
        }
        finally
        {
            if (filePath != null && File.Exists(filePath))
                File.Delete(filePath);
            context.Request.Body.Dispose();
        }
    }

    [Test]
    public async Task Globally_injected_interface_is_resolved_without_body_or_request_service()
    {
        RouterMiddleware.Inject(
            typeof(IGlobalRouteDependency),
            new GlobalRouteDependency("global"));
        var context = CreateContext("GET", "/tests/global-service");

        await RouterMiddleware.OnRequest(context, () => Task.CompletedTask);

        Assert.That(ReadBody(context), Is.EqualTo("global"));
    }

    private static async Task<DefaultHttpContext> CreateMultipartContext(
        string path,
        MultipartFormDataContent multipart)
    {
        var requestBody = new MemoryStream();
        await multipart.CopyToAsync(requestBody);
        requestBody.Position = 0;
        var context = CreateContext("POST", path);
        context.Request.ContentType = multipart.Headers.ContentType!.ToString();
        context.Request.Body = requestBody;
        return context;
    }

    private static DefaultHttpContext CreateContext(
        string method,
        string path,
        IServiceProvider? services = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.RequestServices = services ??
            new ServiceCollection().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context) =>
        Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    [Prefix("/tests")]
    public sealed class TestRouter : Router
    {
        private static TaskCompletionSource concurrentScopeReady = NewScopeSource();
        private static int concurrentScopeCount;

        private static TaskCompletionSource NewScopeSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        [NoRoute]
        public static void ResetConcurrentScope()
        {
            concurrentScopeReady = NewScopeSource();
            concurrentScopeCount = 0;
        }

        [Get]
        [HttpPath("/hello/{name}")]
        public string Hello(string name) => $"Hello {name}";

        [Post]
        [HttpPath("/sum")]
        public object Sum(SumBody body) => new { Result = body.Left + body.Right };

        [Get]
        [HttpPath("/context")]
        public string Context(HttpContext context) => context.Request.Method;

        [Get]
        [HttpPath("/number/{id}")]
        public string Number(int id) => $"Number {id}";

        [Get]
        [HttpPath("/scope")]
        public string Scope(HttpContext context) =>
            $"{context.Request.Method}:{ReferenceEquals(
                context, RouterMiddleware.ContextScope)}";

        [Get]
        [HttpPath("/async")]
        public async Task<object> Async()
        {
            await Task.Yield();
            return new { Value = 12 };
        }

        [Post]
        [HttpPath("/no-content")]
        public void NoContent()
        {
        }

        [Post]
        [HttpPath("/no-content-async")]
        public Task NoContentAsync() => Task.CompletedTask;

        [Get]
        [HttpPath("/service")]
        public string Service(RouteDependency dependency) => dependency.Value;

        [Get]
        [HttpPath("/middleware")]
        [TrackingMiddleware]
        public string WithMiddleware() => "after middleware";

        [Get]
        [HttpPath("/blocked")]
        [BlockingMiddleware]
        public string Blocked() => "must not execute";

        [Get]
        [Post]
        [HttpPath("/multi-a")]
        [HttpPath("/multi-b")]
        public string MultipleRoutes(HttpContext context) =>
            context.Request.Method;

        [Get]
        [HttpPath("/throw-aventus")]
        public string ThrowAventus() =>
            throw new RouteError(
                RouteErrorCode.UnknownError,
                "expected route failure").GetException();

        [Get]
        [HttpPath("/throw-unexpected")]
        public string ThrowUnexpected() =>
            throw new InvalidOperationException("unexpected route failure");

        [Get]
        public string DefaultRoute() => "default route";

        [Get]
        [NoRoute]
        public string HiddenRoute() => "must not be registered";

        [Get]
        [HttpPath("/dynamic/[DynamicSegment]")]
        public string DynamicRoute() => "dynamic route";

        private string DynamicSegment() => "segment";

        [Post]
        [HttpPath("/upload")]
        public object Upload(HttpFile file) => new
        {
            file.FileName,
            file.FilePath,
            Content = File.ReadAllText(file.FilePath),
            file.IsInsideTemp
        };

        [Get]
        [HttpPath("/concurrent-scope/{id}")]
        public async Task<object> ConcurrentScope(
            string id,
            HttpContext context)
        {
            if (Interlocked.Increment(ref concurrentScopeCount) == 2)
                concurrentScopeReady.SetResult();
            await concurrentScopeReady.Task;
            await Task.Yield();
            return new
            {
                Id = id,
                Scope = RouterMiddleware.ContextScope?.Request.Path.ToString()
            };
        }

        [Post]
        [HttpPath("/optional")]
        public string Optional(string? value = null) => value ?? "<null>";

        [Post]
        [HttpPath("/upload-many")]
        public object UploadMany(List<HttpFile> files) =>
            files.Select(file => new
            {
                file.FileName,
                file.FilePath,
                Content = File.ReadAllText(file.FilePath)
            }).ToList();

        [Post]
        [HttpPath("/upload-nested")]
        public object UploadNested(UploadPayload payload) => new
        {
            payload.Name,
            payload.File.FileName,
            payload.File.FilePath,
            Content = File.ReadAllText(payload.File.FilePath)
        };

        [Get]
        [HttpPath("/global-service")]
        public string GlobalService(IGlobalRouteDependency dependency) =>
            dependency.Value;
    }

    public sealed class SumBody
    {
        public int Left { get; set; }
        public int Right { get; set; }
    }

    public sealed record RouteDependency(string Value);

    public interface IGlobalRouteDependency
    {
        string Value { get; }
    }

    public sealed record GlobalRouteDependency(string Value)
        : IGlobalRouteDependency;

    public sealed class UploadPayload
    {
        public string Name { get; set; } = "";
        public HttpFile File { get; set; } = null!;
    }

    public sealed class TrackingMiddlewareAttribute : Middleware
    {
        public static List<string> Routes { get; } = [];

        public static void Reset() => Routes.Clear();

        public override async Task Run(
            HttpContext context,
            RouteInfo info,
            Func<Task> next)
        {
            Routes.Add(info.baseUrl);
            await next();
        }
    }

    public sealed class BlockingMiddlewareAttribute : Middleware
    {
        public override async Task Run(
            HttpContext context,
            RouteInfo info,
            Func<Task> next)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("blocked");
        }
    }

    public sealed class DuplicateRouterOne : Router
    {
        [Get]
        [HttpPath("/duplicate")]
        public string Value() => "one";
    }

    public sealed class DuplicateRouterTwo : Router
    {
        [Get]
        [HttpPath("/duplicate")]
        public string Value() => "two";
    }

    public sealed class RegexRouter : Router
    {
        [Get]
        [PathRegex("^/regex/[A-Z]{2}[0-9]{2}$")]
        public string Match() => "regex";
    }
}
