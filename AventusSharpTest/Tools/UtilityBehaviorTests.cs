using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Tools;

[TestFixture]
public sealed class UtilityBehaviorTests
{
    [TestCase((sbyte)1, true)]
    [TestCase((byte)1, true)]
    [TestCase((short)1, true)]
    [TestCase((ushort)1, true)]
    [TestCase(1, true)]
    [TestCase(1u, true)]
    [TestCase(1L, true)]
    [TestCase(1ul, true)]
    [TestCase(1f, true)]
    [TestCase(1d, true)]
    [TestCase("1", false)]
    [TestCase('1', false)]
    [TestCase(true, false)]
    public void Is_number_recognizes_supported_numeric_runtime_types(
        object value,
        bool expected)
    {
        Assert.That(value.IsNumber(), Is.EqualTo(expected));
    }

    [Test]
    public void Is_number_recognizes_decimal()
    {
        Assert.That(1m.IsNumber(), Is.True);
    }

    [Test]
    public void Current_interfaces_excludes_base_and_transitive_interfaces()
    {
        var result = typeof(DerivedImplementation).GetCurrentInterfaces();

        Assert.That(result, Is.EquivalentTo(new[] { typeof(IDerivedContract) }));
    }

    [Test]
    public void Current_interfaces_returns_direct_interfaces_for_an_interface()
    {
        var result = typeof(ICombinedContract).GetCurrentInterfaces();

        Assert.That(result,
            Is.EquivalentTo(new[] { typeof(IDerivedContract), typeof(ISeparateContract) }));
    }

    [Test]
    public void Generic_error_exception_keeps_the_original_error()
    {
        var error = new GenericError(17, "failed", "source.cs", 42);

        var exception = error.GetException();

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.TypeOf<AventusException>());
            Assert.That(((AventusException)exception).Error, Is.SameAs(error));
            Assert.That(exception.Message, Does.Contain("Error 17: failed"));
            Assert.That(exception.Message, Does.Contain("source.cs:42"));
        });
    }

    [Test]
    public void Generic_error_can_hide_details_from_exception_message()
    {
        var error = new GenericError(2, "invalid", "", -1);
        error.Details.Add("private-detail");

        var hidden = error.GetException(showDetails: false);
        var shown = error.GetException(showDetails: true);

        Assert.Multiple(() =>
        {
            Assert.That(hidden.Message, Does.Not.Contain("private-detail"));
            Assert.That(shown.Message, Does.Contain("private-detail"));
        });
    }

    [Test]
    public void Typed_generic_error_exposes_typed_and_numeric_codes()
    {
        var error = new TypedError(TestErrorCode.Missing, "missing");

        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(TestErrorCode.Missing));
            Assert.That(((GenericError)error).Code, Is.EqualTo(4));
        });
    }

    private interface IBaseContract;
    private interface IDerivedContract : IBaseContract;
    private interface ISeparateContract;
    private interface ICombinedContract : IDerivedContract, ISeparateContract;

    private class BaseImplementation : IBaseContract;
    private sealed class DerivedImplementation : BaseImplementation, IDerivedContract;

    private enum TestErrorCode
    {
        Missing = 4,
    }

    private sealed class TypedError(TestErrorCode code, string message)
        : GenericError<TestErrorCode>(code, message);
}
