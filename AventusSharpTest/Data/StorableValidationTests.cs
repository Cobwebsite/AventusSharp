using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using NUnit.Framework;

namespace AventusSharpTest.Data;

[TestFixture]
public sealed class StorableValidationTests
{
    [TestCase(StorableAction.Create, true)]
    [TestCase(StorableAction.Read, false)]
    [TestCase(StorableAction.Update, true)]
    [TestCase(StorableAction.Delete, false)]
    public void IsValid_forwards_the_requested_action_to_custom_rules(
        StorableAction action,
        bool expectsError)
    {
        var item = new ValidatedRecord { Name = "" };

        var errors = item.IsValid(action);

        Assert.That(errors.Any(), Is.EqualTo(expectsError));
        if (expectsError)
        {
            Assert.That(errors.Single().Code, Is.EqualTo(DataErrorCode.ValidationError));
            Assert.That(errors.Single().Message, Does.Contain(action.ToString()));
        }
    }

    [Test]
    public void IsValid_returns_a_fresh_error_collection_for_each_call()
    {
        var item = new ValidatedRecord();

        var first = item.IsValid(StorableAction.Create);
        first.Clear();
        var second = item.IsValid(StorableAction.Create);

        Assert.That(second, Has.Count.EqualTo(1));
    }
}

[ManualInit]
public sealed class ValidatedRecord : Storable<ValidatedRecord>
{
    public string Name { get; set; } = "";

    protected override List<DataError> ValidationRules(StorableAction action)
    {
        if (string.IsNullOrWhiteSpace(Name) &&
            action is StorableAction.Create or StorableAction.Update)
        {
            return
            [
                new DataError(
                    DataErrorCode.ValidationError,
                    $"Name is required during {action}")
            ];
        }

        return [];
    }
}
