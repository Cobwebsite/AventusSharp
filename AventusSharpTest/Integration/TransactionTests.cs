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

    [Test]
    public async Task Child_async_flow_inherits_transaction_scope_and_rolls_back_with_parent()
    {
        AventusSharp.Data.Manager.TransactionContext? parentContext = null;
        AventusSharp.Data.Manager.TransactionContext? childContext = null;

        var result = await IntegrationEnvironment.Storage.RunInsideTransaction(
            async () =>
            {
                parentContext = IntegrationEnvironment.Storage.getTransactionScope();
                var insertion = await Task.Run(async () =>
                {
                    childContext = IntegrationEnvironment.Storage.getTransactionScope();
                    return await IntegrationEnvironment.Storage.Execute(
                        "INSERT INTO transaction_test (value) VALUES ('child rollback');");
                });
                insertion.Errors.Add(new GenericError(
                    9002, "force parent rollback after child flow"));
                return insertion;
            });
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT value FROM transaction_test;");
        var scopeAfterTransaction =
            IntegrationEnvironment.Storage.getTransactionScope();

        Assert.Multiple(() =>
        {
            Assert.That(parentContext, Is.Not.Null);
            Assert.That(childContext, Is.SameAs(parentContext));
            Assert.That(result.Success, Is.False);
            Assert.That(rows.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(rows.Errors));
            Assert.That(rows.Result, Is.Empty);
            Assert.That(scopeAfterTransaction, Is.Null);
        });
    }

    [Test]
    public async Task Suppressed_execution_context_does_not_leak_the_transaction_scope()
    {
        AventusSharp.Data.Manager.TransactionContext? parentContext = null;
        AventusSharp.Data.Manager.TransactionContext? isolatedContext = null;

        var result = await IntegrationEnvironment.Storage.RunInsideTransaction(
            async () =>
            {
                parentContext = IntegrationEnvironment.Storage.getTransactionScope();
                Task<AventusSharp.Data.Manager.TransactionContext?> isolatedTask;
                using (ExecutionContext.SuppressFlow())
                {
                    isolatedTask = Task.Run(
                        () => IntegrationEnvironment.Storage.getTransactionScope());
                }
                isolatedContext = await isolatedTask;
                return new VoidWithError();
            });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(result.Errors));
            Assert.That(parentContext, Is.Not.Null);
            Assert.That(isolatedContext, Is.Null);
            Assert.That(IntegrationEnvironment.Storage.getTransactionScope(), Is.Null);
        });
    }

    [Test]
    public async Task Invalid_sql_rolls_back_and_does_not_poison_the_next_transaction()
    {
        var failed = await IntegrationEnvironment.Storage.RunInsideTransaction(
            async () =>
            {
                var insertion = await IntegrationEnvironment.Storage.Execute(
                    "INSERT INTO transaction_test (value) VALUES ('before invalid sql');");
                if (!insertion.Success)
                    return insertion;
                return await IntegrationEnvironment.Storage.Execute(
                    "INSERT INTO missing_transaction_table (value) VALUES ('invalid');");
            });
        var afterFailure = await IntegrationEnvironment.Storage.Query(
            "SELECT value FROM transaction_test;");
        var next = await IntegrationEnvironment.Storage.RunInsideTransaction(
            () => IntegrationEnvironment.Storage.Execute(
                "INSERT INTO transaction_test (value) VALUES ('after invalid sql');"));

        Assert.Multiple(() =>
        {
            Assert.That(failed.Success, Is.False);
            Assert.That(afterFailure.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(afterFailure.Errors));
            Assert.That(afterFailure.Result, Is.Empty);
            Assert.That(next.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(next.Errors));
            Assert.That(IntegrationEnvironment.Storage.getTransactionScope(), Is.Null);
        });
    }

    [Test]
    public async Task ReadOnly_write_failure_rolls_back_and_releases_the_transaction_scope()
    {
        IntegrationEnvironment.Storage.ReadOnly = true;
        VoidWithError failed;
        try
        {
            failed = await IntegrationEnvironment.Storage.RunInsideTransaction(
                () => IntegrationEnvironment.Storage.Execute(
                    "INSERT INTO transaction_test (value) VALUES ('read only');"));
        }
        finally
        {
            IntegrationEnvironment.Storage.ReadOnly = false;
        }
        var rows = await IntegrationEnvironment.Storage.Query(
            "SELECT value FROM transaction_test;");
        var next = await IntegrationEnvironment.Storage.RunInsideTransaction(
            () => IntegrationEnvironment.Storage.Execute(
                "INSERT INTO transaction_test (value) VALUES ('writable again');"));

        Assert.Multiple(() =>
        {
            Assert.That(failed.Success, Is.False);
            Assert.That(failed.Errors.OfType<AventusSharp.Data.DataError>()
                .Select(error => error.Code),
                Does.Contain(AventusSharp.Data.DataErrorCode.IsReadOnly));
            Assert.That(rows.Result, Is.Empty);
            Assert.That(next.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(next.Errors));
            Assert.That(IntegrationEnvironment.Storage.getTransactionScope(), Is.Null);
        });
    }
}
