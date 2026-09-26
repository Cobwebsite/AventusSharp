using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataQuotedIdentifierTests
{
    [Test]
    public async Task Crud_supports_reserved_table_and_column_names()
    {
        var clear = await QuotedIdentifierRecord.StartDelete()
            .Where(item => item.Id > 0).RunWithError();
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        var item = new QuotedIdentifierRecord { Value = "first" };
        var creation = await QuotedIdentifierRecord.CreateWithError(item);
        Assert.That(creation.Success, Is.True, IntegrationEnvironment.ErrorMessages(creation.Errors));

        var bulk = await QuotedIdentifierRecord.BulkCreateWithError(
            [new QuotedIdentifierRecord { Value = "second" }]);
        Assert.That(bulk.Success, Is.True, IntegrationEnvironment.ErrorMessages(bulk.Errors));

        item.Value = "updated";
        var update = await QuotedIdentifierRecord.UpdateWithError(item);
        Assert.That(update.Success, Is.True, IntegrationEnvironment.ErrorMessages(update.Errors));

        var query = await QuotedIdentifierRecord.StartQuery()
            .Where(record => record.Value != "missing")
            .Sort(record => record.Value, Sort.ASC).RunWithError();
        Assert.That(query.Success, Is.True, IntegrationEnvironment.ErrorMessages(query.Errors));
        Assert.That(query.Result!.Select(record => record.Value),
            Is.EqualTo(new[] { "second", "updated" }));

        var exists = await QuotedIdentifierRecord.StartExist()
            .Where(record => record.Value == "updated").RunWithError();
        Assert.That(exists.Success, Is.True, IntegrationEnvironment.ErrorMessages(exists.Errors));
        Assert.That(exists.Result, Is.True);

        var deletion = await QuotedIdentifierRecord.StartDelete()
            .Where(record => record.Value == "updated").RunWithError();
        Assert.That(deletion.Success, Is.True, IntegrationEnvironment.ErrorMessages(deletion.Errors));
        var remaining = await QuotedIdentifierRecord.StartQuery().RunWithError();
        Assert.That(remaining.Success, Is.True, IntegrationEnvironment.ErrorMessages(remaining.Errors));
        Assert.That(remaining.Result!.Select(record => record.Value), Is.EqualTo(new[] { "second" }));
    }
}
