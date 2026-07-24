using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Tools;

[TestFixture]
public class ResultWithErrorTests
{
    [Test]
    public void New_result_is_successful()
    {
        var result = new VoidWithError();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void Adding_an_error_marks_result_as_failed()
    {
        var result = new ResultWithError<string> { Result = "ignored" };
        result.Errors.Add(new GenericError(42, "failure"));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors[0].Code, Is.EqualTo(42));
    }

    [Test]
    public void Run_stops_the_pipeline_after_first_error()
    {
        var calls = 0;
        var result = new VoidWithError()
            .Run(() =>
            {
                calls++;
                return new List<GenericError> { new(1, "stop") };
            })
            .Run(() =>
            {
                calls++;
                return new List<GenericError>();
            });

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task Async_pipeline_propagates_result_and_errors()
    {
        var success = await new ResultWithError<int>()
            .RunAsync(async () =>
            {
                await Task.Yield();
                return new ResultWithError<int> { Result = 7 };
            });

        Assert.That(success.Success, Is.True);
        Assert.That(success.Result, Is.EqualTo(7));
    }

    [Test]
    public void Typed_errors_can_be_converted_to_generic_errors()
    {
        var typed = new VoidWithTestError();
        typed.Errors.Add(new TestError(TestErrorCode.Invalid, "invalid"));

        var generic = typed.ToGeneric();

        Assert.That(generic.Success, Is.False);
        Assert.That(generic.Errors.Single().Code, Is.EqualTo((int)TestErrorCode.Invalid));
    }

    private enum TestErrorCode { Invalid = 9 }
    private sealed class TestError(TestErrorCode code, string message) : GenericError<TestErrorCode>(code, message);
    private sealed class VoidWithTestError : VoidWithError<TestError>;
}
