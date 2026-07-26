using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Data;

[TestFixture]
public sealed class TransactionContextTests
{
    [Test]
    public async Task Rollback_actions_run_in_reverse_order_and_continue_after_an_exception()
    {
        var calls = new List<string>();
        var context = new FakeTransactionContext(() =>
        {
            calls.Add("end");
            return Task.CompletedTask;
        });
        context.OnRollback(() =>
        {
            calls.Add("first");
            return Task.FromResult(new VoidWithError());
        });
        context.OnRollback(() =>
            Task.FromException<VoidWithError>(
                new InvalidOperationException("rollback callback failure")));
        context.OnRollback(() =>
        {
            calls.Add("last");
            return Task.FromResult(new VoidWithError());
        });

        var rollback = await context.Rollback();

        Assert.Multiple(() =>
        {
            Assert.That(rollback.Success, Is.False);
            Assert.That(rollback.Result, Is.False);
            Assert.That(rollback.Errors, Has.Count.EqualTo(1));
            Assert.That(rollback.Errors[0].Message, Does.Contain("rollback callback failure"));
            Assert.That(calls, Is.EqualTo(new[] { "last", "first", "end" }));
            Assert.That(context.RollbackCalls, Is.EqualTo(1));
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task Commit_discards_rollback_actions_and_ends_the_context_once()
    {
        var rollbackActionCalls = 0;
        var endCalls = 0;
        var context = new FakeTransactionContext(() =>
        {
            endCalls++;
            return Task.CompletedTask;
        });
        context.OnRollback(() =>
        {
            rollbackActionCalls++;
            return Task.FromResult(new VoidWithError());
        });

        var commit = await context.Commit();
        var secondCommit = await context.Commit();
        var rollbackAfterCommit = await context.Rollback();

        Assert.Multiple(() =>
        {
            Assert.That(commit.Success, Is.True);
            Assert.That(commit.Result, Is.True);
            Assert.That(secondCommit.Result, Is.False);
            Assert.That(rollbackAfterCommit.Result, Is.False);
            Assert.That(context.CommitCalls, Is.EqualTo(1));
            Assert.That(context.RollbackCalls, Is.EqualTo(0));
            Assert.That(rollbackActionCalls, Is.EqualTo(0));
            Assert.That(endCalls, Is.EqualTo(1));
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task Dispose_without_commit_rolls_back_and_disposes_the_transaction()
    {
        var calls = new List<string>();
        var context = new FakeTransactionContext(() =>
        {
            calls.Add("end");
            return Task.CompletedTask;
        });
        context.OnRollback(() =>
        {
            calls.Add("rollback action");
            return Task.FromResult(new VoidWithError());
        });

        await context.DisposeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(new[] { "rollback action", "end" }));
            Assert.That(context.RollbackCalls, Is.EqualTo(1));
            Assert.That(context.DisposeCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Commit_exception_is_returned_and_still_ends_the_context()
    {
        var endCalls = 0;
        var context = new FakeTransactionContext(() =>
        {
            endCalls++;
            return Task.CompletedTask;
        })
        {
            CommitException = new InvalidOperationException("commit failure")
        };

        var commit = await context.Commit();
        var secondCommit = await context.Commit();

        Assert.Multiple(() =>
        {
            Assert.That(commit.Success, Is.False);
            Assert.That(commit.Result, Is.False);
            Assert.That(commit.Errors, Has.Count.EqualTo(1));
            Assert.That(commit.Errors[0].Message, Does.Contain("commit failure"));
            Assert.That(secondCommit.Result, Is.False);
            Assert.That(context.CommitCalls, Is.EqualTo(1));
            Assert.That(endCalls, Is.EqualTo(1),
                "A failed database commit must not leave its transaction scope open.");
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task End_transaction_exception_is_added_to_the_commit_result()
    {
        var context = new FakeTransactionContext(() =>
            Task.FromException(new InvalidOperationException("end transaction failure")));

        var commit = await context.Commit();

        Assert.Multiple(() =>
        {
            Assert.That(commit.Success, Is.False);
            Assert.That(commit.Errors, Has.Count.EqualTo(1));
            Assert.That(commit.Errors[0].Message, Does.Contain("end transaction failure"));
            Assert.That(context.CommitCalls, Is.EqualTo(1));
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task Rollback_exception_is_returned_and_still_ends_the_context()
    {
        var endCalls = 0;
        var context = new FakeTransactionContext(() =>
        {
            endCalls++;
            return Task.CompletedTask;
        })
        {
            RollbackException = new InvalidOperationException("database rollback failure")
        };

        var rollback = await context.Rollback();

        Assert.Multiple(() =>
        {
            Assert.That(rollback.Success, Is.False);
            Assert.That(rollback.Errors, Has.Count.EqualTo(1));
            Assert.That(rollback.Errors[0].Message, Does.Contain("database rollback failure"));
            Assert.That(context.RollbackCalls, Is.EqualTo(1));
            Assert.That(endCalls, Is.EqualTo(1),
                "A failed database rollback must not leave its transaction scope open.");
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task End_transaction_exception_is_added_to_the_rollback_result()
    {
        var context = new FakeTransactionContext(() =>
            Task.FromException(new InvalidOperationException("rollback end failure")));

        var rollback = await context.Rollback();

        Assert.Multiple(() =>
        {
            Assert.That(rollback.Success, Is.False);
            Assert.That(rollback.Errors, Has.Count.EqualTo(1));
            Assert.That(rollback.Errors[0].Message, Does.Contain("rollback end failure"));
            Assert.That(context.RollbackCalls, Is.EqualTo(1));
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task Nested_commits_only_commit_and_end_on_the_outermost_call()
    {
        var endCalls = 0;
        var context = new FakeTransactionContext(() =>
        {
            endCalls++;
            return Task.CompletedTask;
        })
        {
            count = 2
        };

        var innerCommit = await context.Commit();
        var outerCommit = await context.Commit();

        Assert.Multiple(() =>
        {
            Assert.That(innerCommit.Success, Is.True);
            Assert.That(innerCommit.Result, Is.False);
            Assert.That(outerCommit.Success, Is.True);
            Assert.That(outerCommit.Result, Is.True);
            Assert.That(context.CommitCalls, Is.EqualTo(1));
            Assert.That(endCalls, Is.EqualTo(1));
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task Nested_rollback_makes_later_outer_commit_inactive()
    {
        var endCalls = 0;
        var context = new FakeTransactionContext(() =>
        {
            endCalls++;
            return Task.CompletedTask;
        })
        {
            count = 2
        };
        var rollbackActionCalls = 0;
        context.OnRollback(() =>
        {
            rollbackActionCalls++;
            return Task.FromResult(new VoidWithError());
        });

        var innerRollback = await context.Rollback();
        var outerCommit = await context.Commit();
        var secondRollback = await context.Rollback();

        Assert.Multiple(() =>
        {
            Assert.That(innerRollback.Success, Is.True);
            Assert.That(innerRollback.Result, Is.True);
            Assert.That(outerCommit.Result, Is.False);
            Assert.That(secondRollback.Result, Is.False);
            Assert.That(context.RollbackCalls, Is.EqualTo(1));
            Assert.That(context.CommitCalls, Is.EqualTo(0));
            Assert.That(rollbackActionCalls, Is.EqualTo(1));
            Assert.That(endCalls, Is.EqualTo(1));
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task Returned_rollback_errors_are_aggregated_without_stopping_other_actions()
    {
        var calls = new List<string>();
        var context = new FakeTransactionContext(() =>
            Task.FromException(new InvalidOperationException("end after callback errors")));
        context.OnRollback(() =>
        {
            calls.Add("first");
            return Task.FromResult(new VoidWithError());
        });
        context.OnRollback(() =>
        {
            calls.Add("failed");
            return Task.FromResult(new VoidWithError
            {
                Errors = [new GenericError(9901, "returned rollback error")]
            });
        });
        context.OnRollback(() =>
        {
            calls.Add("last");
            return Task.FromResult(new VoidWithError());
        });

        var rollback = await context.Rollback();

        Assert.Multiple(() =>
        {
            Assert.That(rollback.Success, Is.False);
            Assert.That(rollback.Result, Is.False);
            Assert.That(rollback.Errors, Has.Count.EqualTo(2));
            Assert.That(rollback.Errors.Select(error => error.Message),
                Does.Contain("returned rollback error"));
            Assert.That(rollback.Errors.Select(error => error.Message),
                Does.Contain("end after callback errors"));
            Assert.That(calls, Is.EqualTo(new[] { "last", "failed", "first" }));
        });

        await context.DisposeAsync();
    }

    [Test]
    public async Task Dispose_is_idempotent_after_commit_and_after_repeated_calls()
    {
        var context = new FakeTransactionContext(() => Task.CompletedTask);

        var commit = await context.Commit();
        await context.DisposeAsync();
        await context.DisposeAsync();
        context.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(commit.Success, Is.True);
            Assert.That(context.CommitCalls, Is.EqualTo(1));
            Assert.That(context.RollbackCalls, Is.EqualTo(0));
            Assert.That(context.DisposeCalls, Is.EqualTo(1),
                "The underlying transaction must only be disposed once.");
        });
    }

    [Test]
    public async Task Concurrent_dispose_calls_only_dispose_the_driver_once()
    {
        var context = new FakeTransactionContext(() => Task.CompletedTask);
        await context.Commit();

        var disposals = Enumerable.Range(0, 32)
            .Select(_ => context.DisposeAsync().AsTask())
            .ToArray();
        await Task.WhenAll(disposals);

        Assert.That(context.DisposeCalls, Is.EqualTo(1));
    }

    private sealed class FakeTransactionContext(Func<Task> endTransaction)
        : TransactionContext(endTransaction)
    {
        public int CommitCalls { get; private set; }
        public int RollbackCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public Exception? CommitException { get; init; }
        public Exception? RollbackException { get; init; }

        protected override Task TransactionCommit()
        {
            CommitCalls++;
            if (CommitException != null)
                return Task.FromException(CommitException);
            return Task.CompletedTask;
        }

        protected override Task TransactionRollback()
        {
            RollbackCalls++;
            if (RollbackException != null)
                return Task.FromException(RollbackException);
            return Task.CompletedTask;
        }

        protected override Task TransactionDispose()
        {
            DisposeCalls++;
            return Task.CompletedTask;
        }
    }
}
