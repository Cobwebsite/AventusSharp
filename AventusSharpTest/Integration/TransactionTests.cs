using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class TransactionTests
{
    [SetUp]
    public async Task CreateTable()
    {
        var result = await IntegrationEnvironment.Storage.Execute(
            "CREATE TABLE IF NOT EXISTS transaction_test (id INTEGER PRIMARY KEY, value TEXT);" +
            "DELETE FROM transaction_test;");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Successful_transaction_is_committed()
    {
        var result = await IntegrationEnvironment.Storage.RunInsideTransaction(async () =>
        {
            return await IntegrationEnvironment.Storage.Execute(
                "INSERT INTO transaction_test (value) VALUES ('committed');");
        });

        var rows = await IntegrationEnvironment.Storage.Query("SELECT value FROM transaction_test;");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(rows.Result!.Single()["value"], Is.EqualTo("committed"));
    }

    [Test]
    public async Task Failed_transaction_is_rolled_back()
    {
        var result = await IntegrationEnvironment.Storage.RunInsideTransaction(async () =>
        {
            var step = await IntegrationEnvironment.Storage.Execute(
                "INSERT INTO transaction_test (value) VALUES ('rolled-back');");
            step.Errors.Add(new GenericError(9001, "force rollback"));
            return step;
        });

        var rows = await IntegrationEnvironment.Storage.Query("SELECT value FROM transaction_test;");
        Assert.That(result.Success, Is.False);
        Assert.That(rows.Result, Is.Empty);
    }

    [Test]
    public async Task Transaction_callback_exception_is_returned_and_releases_the_scope()
    {
        var failed = await IntegrationEnvironment.Storage.RunInsideTransaction(async () =>
        {
            var insertion = await IntegrationEnvironment.Storage.Execute(
                "INSERT INTO transaction_test (value) VALUES ('before-exception');");
            if (!insertion.Success)
                return insertion;

            throw new InvalidOperationException("storage transaction callback exception");
        });
        var afterFailure = await IntegrationEnvironment.Storage.Query(
            "SELECT value FROM transaction_test;");
        var next = await IntegrationEnvironment.Storage.RunInsideTransaction(
            () => IntegrationEnvironment.Storage.Execute(
                "INSERT INTO transaction_test (value) VALUES ('after-exception');"));

        Assert.That(failed.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(failed.Errors),
            Does.Contain("storage transaction callback exception"));
        Assert.That(afterFailure.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(afterFailure.Errors));
        Assert.That(afterFailure.Result, Is.Empty);
        Assert.That(next.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(next.Errors));
    }
}
