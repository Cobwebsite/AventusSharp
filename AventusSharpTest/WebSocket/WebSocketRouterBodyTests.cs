using AventusSharp.WebSocket;
using AventusSharp.WebSocket.Request;
using NUnit.Framework;

namespace AventusSharpTest.WebSocket;

[TestFixture]
public sealed class WebSocketRouterBodyTests
{
    [Test]
    public void GetData_deserializes_nested_object()
    {
        var body = new WebSocketRouterBody(
            """
            {
              "request": {
                "name": "lamp",
                "value": 42
              }
            }
            """);

        var result = body.GetData(
            typeof(NestedBody),
            "request",
            isOptional: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True,
                ErrorMessages(result.Errors));
            Assert.That(result.Result, Is.TypeOf<NestedBody>());
            var value = (NestedBody)result.Result!;
            Assert.That(value.Name, Is.EqualTo("lamp"));
            Assert.That(value.Value, Is.EqualTo(42));
        });
    }

    [Test]
    public void Missing_required_path_returns_CantGetValueFromBody()
    {
        var body = new WebSocketRouterBody("{}");

        var result = body.GetData(
            typeof(string),
            "missing.value",
            isOptional: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors.Select(error => error.Code),
                Does.Contain(WsErrorCode.CantGetValueFromBody));
            Assert.That(result.Errors.Select(error => error.Message),
                Has.Some.Contains("missing.value"));
        });
    }

    [Test]
    public void Missing_optional_path_succeeds_with_null_result()
    {
        var body = new WebSocketRouterBody("{}");

        var result = body.GetData(
            typeof(string),
            "optional",
            isOptional: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True,
                ErrorMessages(result.Errors));
            Assert.That(result.Result, Is.Null);
        });
    }

    [Test]
    public void Malformed_json_behaves_as_empty_body_without_throwing()
    {
        WebSocketRouterBody? body = null;
        Assert.DoesNotThrow(() =>
            body = new WebSocketRouterBody("{invalid"));

        var result = body!.GetData(
            typeof(string),
            "required",
            isOptional: false);

        Assert.That(result.Errors.Select(error => error.Code),
            Does.Contain(WsErrorCode.CantGetValueFromBody));
    }

    private static string ErrorMessages(
        IEnumerable<AventusSharp.Tools.GenericError> errors) =>
        string.Join(Environment.NewLine,
            errors.Select(error => error.Message));

    private sealed class NestedBody
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }
}
