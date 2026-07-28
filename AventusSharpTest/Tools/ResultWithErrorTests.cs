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

    [Test]
    public void Run_copies_a_successful_nested_result()
    {
        var nested = new ResultWithError<int> { Result = 15 };

        var result = new ResultWithError<int>()
            .Run(() => nested);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result, Is.EqualTo(15));
        });
    }

    [Test]
    public void Run_propagates_nested_errors_without_copying_the_result()
    {
        var nested = new ResultWithError<int> { Result = 15 };
        nested.Errors.Add(new GenericError(3, "nested failure"));

        var result = new ResultWithError<int>()
            .Run(() => nested);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Result, Is.EqualTo(0));
            Assert.That(result.Errors.Single().Message, Is.EqualTo("nested failure"));
        });
    }

    [Test]
    public void Extract_returns_value_and_propagates_failure()
    {
        var successPipeline = new VoidWithError();
        var value = successPipeline.Extract(() =>
            new ResultWithError<string> { Result = "value" });
        var failurePipeline = new VoidWithError();
        var missing = failurePipeline.Extract(() =>
        {
            var result = new ResultWithError<string>();
            result.Errors.Add(new GenericError(8, "missing"));
            return result;
        });

        Assert.Multiple(() =>
        {
            Assert.That(value, Is.EqualTo("value"));
            Assert.That(successPipeline.Success, Is.True);
            Assert.That(missing, Is.Null);
            Assert.That(failurePipeline.Success, Is.False);
        });
    }

    [Test]
    public async Task Async_extension_pipeline_stops_after_an_error()
    {
        var calls = 0;

        var result = await Task.FromResult(new VoidWithError())
            .RunAsync(async () =>
            {
                await Task.Yield();
                calls++;
                return new List<GenericError> { new(1, "stop") };
            })
            .RunAsync(async () =>
            {
                await Task.Yield();
                calls++;
                return new List<GenericError>();
            });

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
        });
    }

    [Test]
    public void To_generic_can_transform_the_result_and_preserve_errors()
    {
        var typed = new ResultWithError<int, TestError> { Result = 7 };
        typed.Errors.Add(new TestError(TestErrorCode.Invalid, "invalid"));

        var generic = typed.ToGeneric(value => $"value:{value}");

        Assert.Multiple(() =>
        {
            Assert.That(generic.Result, Is.EqualTo("value:7"));
            Assert.That(generic.Errors.Single().Code, Is.EqualTo(9));
        });
    }

    private enum TestErrorCode { Invalid = 9 }
    private sealed class TestError(TestErrorCode code, string message) : GenericError<TestErrorCode>(code, message);
    private sealed class VoidWithTestError : VoidWithError<TestError>;
}
