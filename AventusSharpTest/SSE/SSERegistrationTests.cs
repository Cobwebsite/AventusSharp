using System.Reflection;
using AventusSharp.SSE;
using NUnit.Framework;

namespace AventusSharpTest.SSE;

[TestFixture]
[NonParallelizable]
public sealed class SSERegistrationTests
{
    private Dictionary<string, SSEEndPoint> originalEndpoints = null!;
    private object? originalMainEndpoint;

    [SetUp]
    public void SetUp()
    {
        originalEndpoints = SSEMiddleware.endPointInstances
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        originalMainEndpoint = MainField().GetValue(null);
        SSEMiddleware.endPointInstances.Clear();
        MainField().SetValue(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        SSEMiddleware.endPointInstances.Clear();
        foreach (var endpoint in originalEndpoints)
        {
            SSEMiddleware.endPointInstances.Add(endpoint.Key, endpoint.Value);
        }
        MainField().SetValue(null, originalMainEndpoint);
    }

    [Test]
    public void Abstract_endpoints_are_ignored_and_default_endpoint_is_created()
    {
        var result = SSEMiddleware.Register([typeof(AbstractEndpoint)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(SSEMiddleware.endPointInstances, Has.Count.EqualTo(1));
            Assert.That(SSEMiddleware.endPointInstances["/sse"],
                Is.TypeOf<DefaultSSEEndPoint>());
            Assert.That(SSEMiddleware.GetMain(), Is.TypeOf<DefaultSSEEndPoint>());
        });
    }

    [Test]
    public void Duplicate_paths_keep_the_first_registered_endpoint()
    {
        var result = SSEMiddleware.Register(
            [typeof(FirstDuplicateEndpoint), typeof(SecondDuplicateEndpoint)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(SSEMiddleware.endPointInstances, Has.Count.EqualTo(1));
            Assert.That(SSEMiddleware.endPointInstances["/duplicate-sse"],
                Is.TypeOf<FirstDuplicateEndpoint>());
        });
    }

    [Test]
    public void Single_registered_endpoint_becomes_main_automatically()
    {
        var result = SSEMiddleware.Register([typeof(RegularEndpoint)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(SSEMiddleware.GetMain(), Is.TypeOf<RegularEndpoint>());
        });
    }

    [Test]
    public void Explicit_main_endpoint_is_selected_among_multiple_endpoints()
    {
        var result = SSEMiddleware.Register(
            [typeof(RegularEndpoint), typeof(MainEndpoint)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(SSEMiddleware.GetMain(), Is.TypeOf<MainEndpoint>());
            Assert.That(SSEMiddleware.endPointInstances.Keys,
                Is.EquivalentTo(new[] { "/regular-sse", "/main-sse" }));
        });
    }

    [Test]
    public void Multiple_explicit_main_endpoints_are_reported()
    {
        var result = SSEMiddleware.Register(
            [typeof(MainEndpoint), typeof(OtherMainEndpoint)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors.OfType<SSEError>()
                    .Select(error => error.Code),
                Does.Contain(SSEErrorCode.MultipleMainEndpoint));
            Assert.That(SSEMiddleware.GetMain(), Is.TypeOf<MainEndpoint>());
            Assert.That(SSEMiddleware.endPointInstances, Has.Count.EqualTo(2));
        });
    }

    private static FieldInfo MainField()
    {
        return typeof(SSEMiddleware).GetField(
            "mainEndPoint",
            BindingFlags.Static | BindingFlags.NonPublic)!;
    }

    public abstract class AbstractEndpoint : SSEEndPoint
    {
    }

    public sealed class FirstDuplicateEndpoint : SSEEndPoint
    {
        public override string DefinePath() => "/duplicate-sse";
    }

    public sealed class SecondDuplicateEndpoint : SSEEndPoint
    {
        public override string DefinePath() => "/duplicate-sse";
    }

    public sealed class RegularEndpoint : SSEEndPoint
    {
        public override string DefinePath() => "/regular-sse";
    }

    public sealed class MainEndpoint : SSEEndPoint
    {
        public override string DefinePath() => "/main-sse";
        public override bool Main() => true;
    }

    public sealed class OtherMainEndpoint : SSEEndPoint
    {
        public override string DefinePath() => "/other-main-sse";
        public override bool Main() => true;
    }
}
