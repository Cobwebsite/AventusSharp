using System.Text;
using AventusSharp.Routes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using AventusSharpTest.Integration.Models;
using AventusSharp.AspNetCore.Routes;

namespace AventusSharpTest.Integration.Routes;

[TestFixture]
[NonParallelizable]
public sealed class StorableHttpRoutingTests
{
    [OneTimeSetUp]
    public void RegisterRouter()
    {
        var result = RouterMiddleware.Register([typeof(DeviceHttpRouter)]);
        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [SetUp]
    public async Task ClearDevices()
    {
        var result = await Device.StartDelete()
            .Where(device => device.Id > 0)
            .RunWithError();
        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public void Storable_router_registers_all_crud_bulk_and_search_routes()
    {
        var routes = RouterMiddleware.GetAllRoutes()
            .Where(route => route.router is DeviceHttpRouter)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(routes, Has.Count.EqualTo(10));
            AssertRoute(routes, "GET", "/[StorableName]", "/http-device");
            AssertRoute(routes, "POST", "/[StorableName]", "/http-device");
            AssertRoute(routes, "GET", "/[StorableName]/{id}", "/http-device/1");
            AssertRoute(routes, "PUT", "/[StorableName]/{id}", "/http-device/1");
            AssertRoute(routes, "DELETE", "/[StorableName]/{id}", "/http-device/1");
            AssertRoute(routes, "POST", "/[StorableName]s", "/http-devices");
            AssertRoute(routes, "PUT", "/[StorableName]s", "/http-devices");
            AssertRoute(routes, "DELETE", "/[StorableName]s", "/http-devices");
            AssertRoute(
                routes,
                "POST",
                "/[StorableName]/getbyids",
                "/http-device/getbyids");
            AssertRoute(
                routes,
                "POST",
                "/[StorableName]/search",
                "/http-device/search");
        });
    }

    [Test]
    public async Task Storable_router_executes_single_item_crud_lifecycle()
    {
        var create = await SendJson(
            "POST",
            "/http-device",
            """
            {
              "item": {
                "Name": "HTTP lamp",
                "Room": "Office",
                "Brightness": 25,
                "PowerConsumption": 4.5,
                "IsOnline": true
              }
            }
            """);
        var createdJson = JObject.Parse(ReadBody(create));
        var id = createdJson["Result"]?["Id"]?.Value<int>() ?? 0;

        var get = await Send("GET", $"/http-device/{id}");
        var getJson = JObject.Parse(ReadBody(get));
        var update = await SendJson(
            "PUT",
            $"/http-device/{id}",
            """
            {
              "item": {
                "Id": 999999,
                "Name": "Updated HTTP lamp",
                "Room": "Office",
                "Brightness": 70,
                "PowerConsumption": 5.5,
                "IsOnline": false
              }
            }
            """);
        var updateJson = JObject.Parse(ReadBody(update));
        var deletion = await Send("DELETE", $"/http-device/{id}");
        var deletionJson = JObject.Parse(ReadBody(deletion));
        var afterDelete = await Device.GetByIdWithError(id);

        Assert.Multiple(() =>
        {
            Assert.That(create.Response.StatusCode, Is.EqualTo(200));
            Assert.That(createdJson["Success"]?.Value<bool>(), Is.True);
            Assert.That(id, Is.GreaterThan(0));
            Assert.That(getJson["Success"]?.Value<bool>(), Is.True);
            Assert.That(getJson["Result"]?["Name"]?.Value<string>(),
                Is.EqualTo("HTTP lamp"));
            Assert.That(updateJson["Success"]?.Value<bool>(), Is.True);
            Assert.That(updateJson["Result"]?["Id"]?.Value<int>(),
                Is.EqualTo(id), "The route id must override the body id.");
            Assert.That(updateJson["Result"]?["Name"]?.Value<string>(),
                Is.EqualTo("Updated HTTP lamp"));
            Assert.That(deletionJson["Success"]?.Value<bool>(), Is.True);
            Assert.That(deletionJson["Result"]?["Id"]?.Value<int>(),
                Is.EqualTo(id));
            Assert.That(afterDelete.Result, Is.Null);
        });
    }

    [Test]
    public async Task Storable_router_executes_bulk_get_by_ids_and_search_routes()
    {
        var create = await SendJson(
            "POST",
            "/http-devices",
            """
            {
              "list": [
                { "Name": "HTTP Alpha", "Room": "Lab", "Brightness": 10 },
                { "Name": "HTTP Beta", "Room": "Lab", "Brightness": 20 }
              ]
            }
            """);
        var createJson = JObject.Parse(ReadBody(create));
        var ids = createJson["Result"]!
            .Select(item => item["Id"]!.Value<int>())
            .ToArray();

        var getAll = await Send("GET", "/http-device");
        var byIds = await SendJson(
            "POST",
            "/http-device/getbyids",
            $$"""{"ids":[{{ids[1]}},{{ids[0]}}]}""");
        var search = await SendJson(
            "POST",
            "/http-device/search",
            """
            {
              "search": "Beta",
              "fields": ["Name"],
              "limit": 10,
              "page": 0
            }
            """);
        var update = await SendJson(
            "PUT",
            "/http-devices",
            $$"""
            {
              "list": [
                { "Id": {{ids[0]}}, "Name": "HTTP Alpha updated", "Room": "Lab", "Brightness": 30 },
                { "Id": {{ids[1]}}, "Name": "HTTP Beta updated", "Room": "Lab", "Brightness": 40 }
              ]
            }
            """);
        var deletion = await SendJson(
            "DELETE",
            "/http-devices",
            $$"""{"ids":[{{ids[0]}},{{ids[1]}}]}""");

        var getAllJson = JObject.Parse(ReadBody(getAll));
        var byIdsJson = JObject.Parse(ReadBody(byIds));
        var searchJson = JObject.Parse(ReadBody(search));
        var updateJson = JObject.Parse(ReadBody(update));
        var deletionJson = JObject.Parse(ReadBody(deletion));

        Assert.Multiple(() =>
        {
            Assert.That(createJson["Success"]?.Value<bool>(), Is.True);
            Assert.That(ids, Has.Length.EqualTo(2));
            Assert.That(getAllJson["Result"]!
                    .Select(item => item["Id"]!.Value<int>()),
                Is.EquivalentTo(ids));
            Assert.That(byIdsJson["Result"]!
                    .Select(item => item["Id"]!.Value<int>()),
                Is.EquivalentTo(ids));
            Assert.That(searchJson["Result"]!
                    .Select(item => item["Name"]!.Value<string>()),
                Is.EqualTo(new[] { "HTTP Beta" }));
            Assert.That(updateJson["Result"]!
                    .Select(item => item["Name"]!.Value<string>()),
                Is.EquivalentTo(new[]
                {
                    "HTTP Alpha updated",
                    "HTTP Beta updated"
                }));
            Assert.That(deletionJson["Success"]?.Value<bool>(), Is.True);
            Assert.That(deletionJson["Result"]!
                    .Select(item => item["Id"]!.Value<int>()),
                Is.EquivalentTo(ids));
        });
    }

    private static void AssertRoute(
        IEnumerable<RouteInfo> routes,
        string method,
        string template,
        string resolvedPath)
    {
        Assert.That(routes.Any(route =>
            route.baseUrl == template &&
            route.method.ToString().Equals(
                method,
                StringComparison.OrdinalIgnoreCase) &&
            route.pattern.IsMatch(resolvedPath)), Is.True,
            $"Missing {method} {template} ({resolvedPath}). Registered: " +
            string.Join(", ", routes.Select(route =>
                $"{route.method} {route.baseUrl}")));
    }

    private static async Task<DefaultHttpContext> Send(
        string method,
        string path)
    {
        var context = CreateContext(method, path);
        await RouterAdapter.OnRequest(
            context,
            () => Task.CompletedTask);
        return context;
    }

    private static async Task<DefaultHttpContext> SendJson(
        string method,
        string path,
        string json)
    {
        var context = CreateContext(method, path);
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(
            Encoding.UTF8.GetBytes(json));
        await RouterAdapter.OnRequest(
            context,
            () => Task.CompletedTask);
        return context;
    }

    private static DefaultHttpContext CreateContext(
        string method,
        string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.RequestServices =
            new ServiceCollection().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context) =>
        Encoding.UTF8.GetString(
            ((MemoryStream)context.Response.Body).ToArray());

    public sealed class DeviceHttpRouter : StorableRouter<Device>
    {
        protected override string StorableName() => "http-device";
    }
}
