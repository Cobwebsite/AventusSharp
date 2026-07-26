using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataSqlTransformQueryTests
{
    [SetUp]
    public async Task SetUp()
    {
        var reset = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"transformed_bool_records\";");
        Assert.That(reset.Success, Is.True, IntegrationEnvironment.ErrorMessages(reset.Errors));
    }

    [Test]
    public async Task Boolean_transform_round_trips_as_yes_and_no()
    {
        var active = await TransformedBoolRecord.Create(new TransformedBoolRecord
        {
            Name = "Active",
            Deleted = false
        });
        var deleted = await TransformedBoolRecord.Create(new TransformedBoolRecord
        {
            Name = "Deleted",
            Deleted = true
        });
        var raw = await IntegrationEnvironment.Storage.Query(
            "SELECT \"Name\", \"Deleted\" FROM \"transformed_bool_records\" ORDER BY \"Name\";");
        var manager = (TransformedBoolRecordManager)GenericDM.Get<TransformedBoolRecord>();
        var loadedActive = await manager.GetByIdWithErrorNoCache<TransformedBoolRecord>(active!.Id);
        var loadedDeleted = await manager.GetByIdWithErrorNoCache<TransformedBoolRecord>(deleted!.Id);

        Assert.That(raw.Success, Is.True, IntegrationEnvironment.ErrorMessages(raw.Errors));
        Assert.That(raw.Result!.Single(row => row["Name"] == "Active")["Deleted"], Is.EqualTo("N"));
        Assert.That(raw.Result!.Single(row => row["Name"] == "Deleted")["Deleted"], Is.EqualTo("Y"));
        Assert.That(loadedActive.Result!.Deleted, Is.False);
        Assert.That(loadedDeleted.Result!.Deleted, Is.True);
    }

    [Test]
    [Explicit("Specification: LambdaTranslator does not yet transform a negated boolean member to its SQL representation.")]
    public async Task Negated_boolean_member_uses_the_transformed_false_value()
    {
        await Seed();

        var result = await TransformedBoolRecord.StartQuery()
            .Where(item => !item.Deleted)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name), Is.EqualTo(new[] { "Active" }));
    }

    [Test]
    [Explicit("Specification: LambdaTranslator does not yet transform a boolean member to its SQL representation.")]
    public async Task Boolean_member_uses_the_transformed_true_value()
    {
        await Seed();

        var result = await TransformedBoolRecord.StartQuery()
            .Where(item => item.Deleted)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name), Is.EqualTo(new[] { "Deleted" }));
    }

    [TestCase(false, "Active")]
    [TestCase(true, "Deleted")]
    [Explicit("Specification: captured query values are not yet passed through the field SqlTransform.")]
    public async Task Captured_boolean_comparison_uses_the_field_transform(
        bool deleted,
        string expectedName)
    {
        await Seed();

        var result = await TransformedBoolRecord.StartQuery()
            .Where(item => item.Deleted == deleted)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name), Is.EqualTo(new[] { expectedName }));
    }

    private static async Task Seed()
    {
        var creation = await TransformedBoolRecord.CreateWithError(
        [
            new TransformedBoolRecord { Name = "Active", Deleted = false },
            new TransformedBoolRecord { Name = "Deleted", Deleted = true }
        ]);
        Assert.That(creation.Success, Is.True, IntegrationEnvironment.ErrorMessages(creation.Errors));
    }
}
