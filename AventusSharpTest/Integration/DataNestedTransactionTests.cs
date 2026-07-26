using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataNestedTransactionTests
{
    private IGenericDM Manager => GenericDM.Get<Device>();

    [SetUp]
    public async Task ClearTable()
    {
        var result = await Device.StartDelete()
            .Where(device => device.Id > 0)
            .RunWithError();
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Successful_nested_transaction_commits_all_operations()
    {
        var result = await Manager.RunInsideTransaction(async () =>
        {
            var first = await Device.CreateWithError(NewDevice("Outer before"));
            if (!first.Success)
                return first;

            var inner = await Manager.RunInsideTransaction(
                () => Device.CreateWithError(NewDevice("Inner")));
            if (!inner.Success)
                return inner;

            return await Device.CreateWithError(NewDevice("Outer after"));
        });
        var loaded = await ((DeviceManager)Manager)
            .WhereWithErrorNoCache<Device>(device => device.Room == "Nested");

        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Select(device => device.Name),
            Is.EquivalentTo(new[] { "Outer before", "Inner", "Outer after" }));
    }

    [Test]
    public async Task Failed_outer_transaction_rolls_back_operations_after_inner_commit()
    {
        var result = await Manager.RunInsideTransaction(async () =>
        {
            var first = await Device.CreateWithError(NewDevice("Outer before"));
            if (!first.Success)
                return first;

            var inner = await Manager.RunInsideTransaction(
                () => Device.CreateWithError(NewDevice("Inner")));
            if (!inner.Success)
                return inner;

            var last = await Device.CreateWithError(NewDevice("Outer after"));
            last.Errors.Add(new GenericError(9920, "force outer rollback"));
            return last;
        });
        var loaded = await ((DeviceManager)Manager)
            .WhereWithErrorNoCache<Device>(device => device.Room == "Nested");

        Assert.That(result.Success, Is.False);
        Assert.That(loaded.Result, Is.Empty);
    }

    [Test]
    [Explicit("Specification: an inner rollback must poison the outer transaction even when its failed result is ignored.")]
    public async Task Failed_inner_transaction_cannot_be_ignored_by_the_outer_callback()
    {
        var innerFailureObserved = false;
        var result = await Manager.RunInsideTransaction(async () =>
        {
            var before = await Device.CreateWithError(NewDevice("Before inner failure"));
            if (!before.Success)
                return before;

            var inner = await Manager.RunInsideTransaction(async () =>
            {
                var created = await Device.CreateWithError(NewDevice("Inner failure"));
                created.Errors.Add(new GenericError(9921, "force inner rollback"));
                return created;
            });
            innerFailureObserved = !inner.Success;

            return await Device.CreateWithError(NewDevice("After inner failure"));
        });
        var loaded = await ((DeviceManager)Manager)
            .WhereWithErrorNoCache<Device>(device => device.Room == "Nested");

        Assert.That(innerFailureObserved, Is.True);
        Assert.That(result.Success, Is.False,
            "The outer result must report that its shared transaction was rolled back.");
        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result, Is.Empty,
            "No operation may commit after a rollback of the shared nested transaction.");
    }

    [Test]
    public async Task Propagated_inner_failure_rolls_back_outer_database_and_cache_changes()
    {
        Device? createdBeforeInnerFailure = null;
        var result = await Manager.RunInsideTransaction(async () =>
        {
            var before = await Device.CreateWithError(NewDevice("Before propagated failure"));
            if (!before.Success)
                return before;
            createdBeforeInnerFailure = before.Result;

            return await Manager.RunInsideTransaction(async () =>
            {
                var inner = await Device.CreateWithError(NewDevice("Propagated inner failure"));
                inner.Errors.Add(new GenericError(9922, "force propagated inner rollback"));
                return inner;
            });
        });
        var loaded = await ((DeviceManager)Manager)
            .WhereWithErrorNoCache<Device>(device => device.Room == "Nested");
        var cached = await Device.GetByIdWithError(createdBeforeInnerFailure!.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result, Is.Empty);
        Assert.That(cached.Success, Is.False);
        Assert.That(cached.Result, Is.Null);
    }

    [Test]
    public async Task Transaction_callback_exception_rolls_back_and_releases_the_scope()
    {
        Device? createdBeforeException = null;
        var failed = await Manager.RunInsideTransaction<Device>(async () =>
        {
            var creation = await Device.CreateWithError(NewDevice("Before exception"));
            if (!creation.Success)
                return creation;

            createdBeforeException = creation.Result;
            throw new InvalidOperationException("transaction callback exception");
        });
        var afterFailure = await ((DeviceManager)Manager)
            .WhereWithErrorNoCache<Device>(device => device.Room == "Nested");
        var cachedAfterFailure = await Device.GetByIdWithError(createdBeforeException!.Id);
        var next = await Manager.RunInsideTransaction(
            () => Device.CreateWithError(NewDevice("After exception")));

        Assert.That(failed.Success, Is.False);
        Assert.That(IntegrationEnvironment.ErrorMessages(failed.Errors),
            Does.Contain("transaction callback exception"));
        Assert.That(afterFailure.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(afterFailure.Errors));
        Assert.That(afterFailure.Result, Is.Empty);
        Assert.That(cachedAfterFailure.Success, Is.False);
        Assert.That(cachedAfterFailure.Result, Is.Null);
        Assert.That(next.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(next.Errors));
    }

    [Test]
    public async Task Transaction_callback_exception_restores_updated_cached_values()
    {
        var device = await Device.Create(NewDevice("Updated before exception"));
        Assert.That(device, Is.Not.Null);
        var cached = await Device.GetByIdWithError(device!.Id);
        Assert.That(cached.Result, Is.SameAs(device));

        var failed = await Manager.RunInsideTransaction<Device>(async () =>
        {
            device.Brightness = 99;
            var update = await Device.UpdateWithError(device);
            if (!update.Success)
                return update;

            throw new InvalidOperationException("update transaction callback exception");
        });
        var stored = await ((DeviceManager)Manager)
            .GetByIdWithErrorNoCache<Device>(device.Id);
        var cachedAfterFailure = await Device.GetByIdWithError(device.Id);

        Assert.That(failed.Success, Is.False);
        Assert.That(stored.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(stored.Errors));
        Assert.That(stored.Result!.Brightness, Is.EqualTo(1));
        Assert.That(cachedAfterFailure.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(cachedAfterFailure.Errors));
        Assert.That(cachedAfterFailure.Result, Is.SameAs(device));
        Assert.That(device.Brightness, Is.EqualTo(1));
    }

    private static Device NewDevice(string name) =>
        new()
        {
            Name = name,
            Room = "Nested",
            Brightness = 1,
            PowerConsumption = 1,
            IsOnline = true,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 1, 1)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 1, 1, 10, 0, 0))
        };
}
