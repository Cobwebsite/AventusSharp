using System.Security.Claims;
using AventusSharp.Hosting;
using NUnit.Framework;

namespace AventusSharpTest.Hosting;

public sealed class AventusContextTests
{
    [Test]
    public void Defaults_are_suitable_for_an_in_process_request()
    {
        var request = new AventusRequest();
        using var response = new AventusResponse();
        var services = new EmptyServiceProvider();
        var context = new AventusContext(request, response, services);

        Assert.That(request.Method, Is.EqualTo("GET"));
        Assert.That(request.Path, Is.EqualTo("/"));
        Assert.That(response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Body, Is.TypeOf<MemoryStream>());
        Assert.That(context.Services, Is.SameAs(services));
        Assert.That(context.User, Is.Not.Null);
        Assert.That(context.Items, Is.Empty);
    }

    [Test]
    public void Host_state_can_be_provided_without_AspNetCore_types()
    {
        var request = new AventusRequest
        {
            Method = "POST",
            Path = "/api/items",
            QueryString = "?page=2",
            ContentType = "application/json",
            Body = new MemoryStream([1, 2, 3])
        };
        request.Headers["X-Test"] = ["one", "two"];

        using var response = new AventusResponse();
        var context = new AventusContext(request, response, new EmptyServiceProvider())
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "test")]))
        };

        Assert.That(context.Request.Method, Is.EqualTo("POST"));
        Assert.That(context.Request.Headers["X-Test"], Is.EqualTo(new[] { "one", "two" }));
        Assert.That(context.User.Identity?.Name, Is.EqualTo("test"));
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
