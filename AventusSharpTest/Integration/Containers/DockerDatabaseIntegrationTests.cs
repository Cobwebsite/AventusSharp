using AventusSharp.Data;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Integration.Containers;

[TestFixture]
[Category("Docker")]
[NonParallelizable]
public sealed class DockerDatabaseIntegrationTests
{
    [SetUp]
    public void RequireDocker()
    {
        DatabaseContainers.RequireDocker();
    }

    [Test]
    public async Task MySql_generates_schema_and_executes_crud_and_lambda_queries()
    {
        await VerifyProvider(
            "mysql_devices",
            () => MySqlDevice.CreateWithError(new MySqlDevice
            {
                Name = "MySQL lamp", Room = "Office", Value = 40, Enabled = true,
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Duration = new TimeSpan(12, 34, 56)
            }),
            () => MySqlDevice.GetAllWithError(),
            value => MySqlDevice.Where(device =>
                device.Room == "Office" && device.Value >= value && device.Enabled),
            device =>
            {
                device.Value = 70;
                return MySqlDevice.UpdateWithError(device);
            },
            device => MySqlDevice.DeleteWithError(device));
    }

    [Test]
    public async Task PostgreSql_generates_schema_and_executes_crud_and_lambda_queries()
    {
        await VerifyProvider(
            "postgresql_devices",
            () => PostgreSqlDevice.CreateWithError(new PostgreSqlDevice
            {
                Name = "PostgreSQL lamp", Room = "Office", Value = 40, Enabled = true,
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Duration = new TimeSpan(12, 34, 56)
            }),
            () => PostgreSqlDevice.GetAllWithError(),
            value => PostgreSqlDevice.Where(device =>
                device.Room == "Office" && device.Value >= value && device.Enabled),
            device =>
            {
                device.Value = 70;
                return PostgreSqlDevice.UpdateWithError(device);
            },
            device => PostgreSqlDevice.DeleteWithError(device));
    }

    [Test]
    public async Task SqlServer_generates_schema_and_executes_crud_and_lambda_queries()
    {
        await VerifyProvider(
            "mssql_devices",
            () => MsSqlDevice.CreateWithError(new MsSqlDevice
            {
                Name = "SQL Server lamp", Room = "Office", Value = 40, Enabled = true,
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Duration = new TimeSpan(12, 34, 56)
            }),
            () => MsSqlDevice.GetAllWithError(),
            value => MsSqlDevice.Where(device =>
                device.Room == "Office" && device.Value >= value && device.Enabled),
            device =>
            {
                device.Value = 70;
                return MsSqlDevice.UpdateWithError(device);
            },
            device => MsSqlDevice.DeleteWithError(device));
    }

    [TestCaseSource(nameof(ProviderSchemas))]
    public async Task Providers_generate_size_nullability_and_string_defaults(
        string tableName)
    {
        IDBStorage storage = tableName switch
        {
            "mysql_devices" => DatabaseContainers.MySql,
            "postgresql_devices" => DatabaseContainers.PostgreSql,
            "mssql_devices" => DatabaseContainers.MsSql,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        var result = await storage.Query(
            "SELECT CHARACTER_MAXIMUM_LENGTH AS max_length, " +
            "IS_NULLABLE AS is_nullable, COLUMN_DEFAULT AS default_value " +
            "FROM INFORMATION_SCHEMA.COLUMNS " +
            $"WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'Category';");
        var nameResult = await storage.Query(
            "SELECT CHARACTER_MAXIMUM_LENGTH AS max_length, " +
            "IS_NULLABLE AS is_nullable " +
            "FROM INFORMATION_SCHEMA.COLUMNS " +
            $"WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'Name';");

        Assert.That(result.Success, Is.True,
            string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.That(nameResult.Success, Is.True,
            string.Join(Environment.NewLine, nameResult.Errors.Select(error => error.Message)));
        var category = result.Result!.Single();
        var name = nameResult.Result!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(name["max_length"], Is.EqualTo("64"), $"{tableName}.Name size");
            Assert.That(name["is_nullable"], Is.EqualTo("NO").IgnoreCase,
                $"{tableName}.Name nullability");
            Assert.That(category["is_nullable"], Is.EqualTo("NO").IgnoreCase,
                $"{tableName}.Category nullability");
            Assert.That(category["default_value"], Does.Contain("standard").IgnoreCase,
                $"{tableName}.Category default");
        });
    }

    private static IEnumerable<TestCaseData> ProviderSchemas()
    {
        yield return new TestCaseData("mysql_devices")
            .SetName("MySQL_schema_contains_size_nullability_and_default");
        yield return new TestCaseData("postgresql_devices")
            .SetName("PostgreSQL_schema_contains_size_nullability_and_default");
        yield return new TestCaseData("mssql_devices")
            .SetName("SQL_Server_schema_contains_size_nullability_and_default");
    }

    [Test]
    public Task MySql_rolls_back_manager_transactions()
    {
        return VerifyRollback(
            () => MySqlDevice.CreateWithError(new MySqlDevice
            {
                Name = "mysql rollback", Room = "Lab",
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }),
            () => ((MySqlDeviceManager)GenericDM.Get<MySqlDevice>())
                .WhereWithErrorNoCache<MySqlDevice>(device => device.Name == "mysql rollback"));
    }

    [Test]
    public Task PostgreSql_rolls_back_manager_transactions()
    {
        return VerifyRollback(
            () => PostgreSqlDevice.CreateWithError(new PostgreSqlDevice
            {
                Name = "postgres rollback", Room = "Lab",
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }),
            () => ((PostgreSqlDeviceManager)GenericDM.Get<PostgreSqlDevice>())
                .WhereWithErrorNoCache<PostgreSqlDevice>(device => device.Name == "postgres rollback"));
    }

    [Test]
    public Task SqlServer_rolls_back_manager_transactions()
    {
        return VerifyRollback(
            () => MsSqlDevice.CreateWithError(new MsSqlDevice
            {
                Name = "mssql rollback", Room = "Lab",
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }),
            () => ((MsSqlDeviceManager)GenericDM.Get<MsSqlDevice>())
                .WhereWithErrorNoCache<MsSqlDevice>(device => device.Name == "mssql rollback"));
    }

    [Test]
    public async Task Rollback_removes_a_created_item_from_the_local_cache()
    {
        var manager = GenericDM.Get<MySqlDevice>();
        MySqlDevice? created = null;
        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var result = await MySqlDevice.CreateWithError(new MySqlDevice
            {
                Name = "cache rollback",
                Room = "Lab",
                InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            created = result.Result;
            result.Errors.Add(new GenericError(9902, "forced cache rollback"));
            return result;
        });

        Assert.That(transaction.Success, Is.False);
        Assert.That(created, Is.Not.Null);

        var cachedLookup = await manager.GetByIdWithError<MySqlDevice>(created!.Id);
        Assert.That(cachedLookup.Result, Is.Null,
            "The rolled-back item must not remain addressable through preferLocalCache.");
    }

    [Test]
    public Task MySql_nested_outer_rollback_includes_operations_after_inner_commit() =>
        VerifyNestedRollback<MySqlDevice>();

    [Test]
    public Task PostgreSql_nested_outer_rollback_includes_operations_after_inner_commit() =>
        VerifyNestedRollback<PostgreSqlDevice>();

    [Test]
    public Task SqlServer_nested_outer_rollback_includes_operations_after_inner_commit() =>
        VerifyNestedRollback<MsSqlDevice>();

    [Test]
    public Task MySql_supports_direct_relations_and_cascade() =>
        VerifyDirectRelation(
            (MySqlRelationChild child) => child.Parent,
            parent => new MySqlRelationChild { Name = "MySQL child", Parent = parent },
            (child, parent) => child.Parent = parent);

    [Test]
    public Task PostgreSql_supports_direct_relations_and_cascade() =>
        VerifyDirectRelation(
            (PostgreSqlRelationChild child) => child.Parent,
            parent => new PostgreSqlRelationChild
            {
                Name = "PostgreSQL child",
                Parent = parent
            },
            (child, parent) => child.Parent = parent);

    [Test]
    public Task SqlServer_supports_direct_relations_and_cascade() =>
        VerifyDirectRelation(
            (MsSqlRelationChild child) => child.Parent,
            parent => new MsSqlRelationChild { Name = "SQL Server child", Parent = parent },
            (child, parent) => child.Parent = parent);

    [Test]
    public Task MySql_supports_many_to_many_relation_lifecycle() =>
        VerifyManyToManyRelation<
            MySqlRelationParent,
            MySqlRelationChild,
            MySqlRelationGroup>(
            parent => new MySqlRelationChild { Name = "MySQL linked child", Parent = parent },
            group => group.Children,
            (group, children) => group.Children = children);

    [Test]
    public Task PostgreSql_supports_many_to_many_relation_lifecycle() =>
        VerifyManyToManyRelation<
            PostgreSqlRelationParent,
            PostgreSqlRelationChild,
            PostgreSqlRelationGroup>(
            parent => new PostgreSqlRelationChild
            {
                Name = "PostgreSQL linked child",
                Parent = parent
            },
            group => group.Children,
            (group, children) => group.Children = children);

    [Test]
    public Task SqlServer_supports_many_to_many_relation_lifecycle() =>
        VerifyManyToManyRelation<
            MsSqlRelationParent,
            MsSqlRelationChild,
            MsSqlRelationGroup>(
            parent => new MsSqlRelationChild
            {
                Name = "SQL Server linked child",
                Parent = parent
            },
            group => group.Children,
            (group, children) => group.Children = children);

    private static async Task VerifyProvider<T>(
        string expectedTable,
        Func<Task<ResultWithError<T>>> create,
        Func<Task<ResultWithError<List<T>>>> getAll,
        Func<int, Task<List<T>>> query,
        Func<T, Task<ResultWithError<T>>> update,
        Func<T, Task<ResultWithError<T>>> delete)
        where T : class, IStorable
    {
        var createResult = await create();
        var errorMessage = string.Join(Environment.NewLine,
            createResult.Errors.Select(error => error.GetMessageException(true)));
        Assert.That(createResult.Success, Is.True, $"{expectedTable}: create failed{Environment.NewLine}{errorMessage}");
        var created = createResult.Result;
        Assert.That(created, Is.Not.Null, $"{expectedTable}: create returned no item");
        Assert.That(created!.Id, Is.GreaterThan(0), $"{expectedTable}: no generated id");
        Assert.That(created, Is.InstanceOf<IStorableTimestamp>());
        var timestamped = (IStorableTimestamp)created;
        var createdDate = timestamped.CreatedDate;
        var firstUpdatedDate = timestamped.UpdatedDate;
        Assert.That(createdDate, Is.GreaterThan(DateTime.MinValue), $"{expectedTable}: no creation timestamp");
        Assert.That(firstUpdatedDate, Is.GreaterThan(DateTime.MinValue), $"{expectedTable}: no update timestamp");

        var getAllResult = await getAll();
        var getAllErrors = string.Join(Environment.NewLine,
            getAllResult.Errors.Select(error => error.GetMessageException(true)));
        Assert.That(getAllResult.Success, Is.True,
            $"{expectedTable}: get-all failed{Environment.NewLine}{getAllErrors}");
        var all = getAllResult.Result ?? [];
        Assert.That(all.Any(item => item.Id == created.Id), Is.True,
            $"{expectedTable}: generated schema/query failed; created id={created.Id}; "
            + $"loaded ids=[{string.Join(",", all.Select(item => item.Id))}]");
        Assert.That(((IContainerDevice)all.Single(item => item.Id == created.Id)).Duration,
            Is.EqualTo(new TimeSpan(12, 34, 56)),
            $"{expectedTable}: TimeSpan did not round-trip");

        var filtered = await query(30);
        Assert.That(filtered.Any(item => item.Id == created.Id), Is.True,
            $"{expectedTable}: LambdaTranslator query failed");

        await Task.Delay(20);
        var updateResult = await update(created);
        Assert.That(updateResult.Success, Is.True, $"{expectedTable}: update failed{Environment.NewLine}"
            + string.Join(Environment.NewLine, updateResult.Errors.Select(error => error.GetMessageException(true))));
        Assert.That(timestamped.CreatedDate, Is.EqualTo(createdDate),
            $"{expectedTable}: update changed CreatedDate");
        Assert.That(timestamped.UpdatedDate, Is.GreaterThan(firstUpdatedDate),
            $"{expectedTable}: update did not advance UpdatedDate");

        var deleteResult = await delete(created);
        Assert.That(deleteResult.Success, Is.True, $"{expectedTable}: delete failed{Environment.NewLine}"
            + string.Join(Environment.NewLine, deleteResult.Errors.Select(error => error.GetMessageException(true))));
    }

    private static async Task VerifyRollback<T>(
        Func<Task<ResultWithError<T>>> create,
        Func<Task<ResultWithError<List<T>>>> find)
        where T : class, IStorable
    {
        var manager = GenericDM.Get<T>();
        var transactionResult = await manager.RunInsideTransaction(async () =>
        {
            var result = await create();
            result.Errors.Add(new GenericError(9901, "forced rollback"));
            return result;
        });

        Assert.That(transactionResult.Success, Is.False);

        var queryResult = await find();
        Assert.That(queryResult.Success, Is.True,
            string.Join(Environment.NewLine, queryResult.Errors.Select(error => error.GetMessageException(true))));
        Assert.That(queryResult.Result, Is.Empty, "The row created in the failed transaction was persisted.");
    }

    private static async Task VerifyNestedRollback<T>()
        where T : class, IStorable, IContainerDevice, new()
    {
        var manager = GenericDM.Get<T>();
        var cleanup = await manager.CreateDelete<T>()
            .Where(item => item.Id > 0)
            .RunWithError();
        Assert.That(cleanup.Success, Is.True,
            string.Join(Environment.NewLine, cleanup.Errors.Select(error => error.Message)));

        var transaction = await manager.RunInsideTransaction(async () =>
        {
            var first = await manager.CreateWithError(NewNestedDevice<T>("Outer before"));
            if (!first.Success)
                return first;

            var inner = await manager.RunInsideTransaction(
                () => manager.CreateWithError(NewNestedDevice<T>("Inner")));
            if (!inner.Success)
                return inner;

            var last = await manager.CreateWithError(NewNestedDevice<T>("Outer after"));
            last.Errors.Add(new GenericError(9921, "force nested rollback"));
            return last;
        });
        var loaded = await manager.CreateQuery<T>()
            .Where(item => item.Room == "Nested")
            .RunWithError();

        Assert.That(transaction.Success, Is.False);
        Assert.That(loaded.Success, Is.True,
            string.Join(Environment.NewLine, loaded.Errors.Select(error => error.Message)));
        Assert.That(loaded.Result, Is.Empty);
    }

    private static T NewNestedDevice<T>(string name)
        where T : class, IContainerDevice, new() =>
        new()
        {
            Name = name,
            Category = "nested",
            Room = "Nested",
            Value = 1,
            Enabled = true,
            InstalledAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Duration = TimeSpan.FromMinutes(1)
        };

    private static async Task VerifyDirectRelation<TParent, TChild>(
        Func<TChild, TParent> getParent,
        Func<TParent, TChild> createChild,
        Action<TChild, TParent> setParent)
        where TParent : class, IStorable, new()
        where TChild : class, IStorable
    {
        var parentManager = GenericDM.Get<TParent>();
        var childManager = GenericDM.Get<TChild>();
        var deleteChildren = await childManager.CreateDelete<TChild>()
            .Where(item => item.Id > 0)
            .RunWithError();
        var deleteParents = await parentManager.CreateDelete<TParent>()
            .Where(item => item.Id > 0)
            .RunWithError();
        Assert.That(deleteChildren.Success, Is.True,
            string.Join(Environment.NewLine, deleteChildren.Errors.Select(error => error.Message)));
        Assert.That(deleteParents.Success, Is.True,
            string.Join(Environment.NewLine, deleteParents.Errors.Select(error => error.Message)));

        var firstParent = new TParent();
        typeof(TParent).GetProperty("Name")!.SetValue(firstParent, "First parent");
        var secondParent = new TParent();
        typeof(TParent).GetProperty("Name")!.SetValue(secondParent, "Second parent");
        var firstCreation = await parentManager.CreateWithError(firstParent);
        var secondCreation = await parentManager.CreateWithError(secondParent);
        Assert.That(firstCreation.Success, Is.True,
            string.Join(Environment.NewLine, firstCreation.Errors.Select(error => error.Message)));
        Assert.That(secondCreation.Success, Is.True,
            string.Join(Environment.NewLine, secondCreation.Errors.Select(error => error.Message)));

        var child = createChild(firstParent);
        var childCreation = await childManager.CreateWithError(child);
        Assert.That(childCreation.Success, Is.True,
            string.Join(Environment.NewLine, childCreation.Errors.Select(error => error.Message)));

        var initiallyLoaded = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == child.Id)
            .SingleWithError();
        Assert.That(initiallyLoaded.Success, Is.True,
            string.Join(Environment.NewLine, initiallyLoaded.Errors.Select(error => error.Message)));
        Assert.That(getParent(initiallyLoaded.Result!).Id, Is.EqualTo(firstParent.Id));

        setParent(child, secondParent);
        var update = await childManager.UpdateWithError(child);
        var afterUpdate = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == child.Id)
            .SingleWithError();
        Assert.That(update.Success, Is.True,
            string.Join(Environment.NewLine, update.Errors.Select(error => error.Message)));
        Assert.That(afterUpdate.Success, Is.True,
            string.Join(Environment.NewLine, afterUpdate.Errors.Select(error => error.Message)));
        Assert.That(getParent(afterUpdate.Result!).Id, Is.EqualTo(secondParent.Id));

        var invalidParent = new TParent { Id = 999_999 };
        setParent(child, invalidParent);
        var invalidUpdate = await childManager.UpdateWithError(child);
        var afterInvalidUpdate = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == child.Id)
            .SingleWithError();
        Assert.That(invalidUpdate.Success, Is.False);
        Assert.That(afterInvalidUpdate.Success, Is.True,
            string.Join(Environment.NewLine,
                afterInvalidUpdate.Errors.Select(error => error.Message)));
        Assert.That(getParent(afterInvalidUpdate.Result!).Id, Is.EqualTo(secondParent.Id));
        Assert.That(getParent(child), Is.SameAs(secondParent),
            "A failed direct relation update must restore the canonical relation.");

        var parentDeletion = await parentManager.DeleteWithError(secondParent);
        var childAfterCascade = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == child.Id)
            .SingleWithError();
        Assert.That(parentDeletion.Success, Is.True,
            string.Join(Environment.NewLine, parentDeletion.Errors.Select(error => error.Message)));
        Assert.That(childAfterCascade.Success, Is.True,
            string.Join(Environment.NewLine, childAfterCascade.Errors.Select(error => error.Message)));
        Assert.That(childAfterCascade.Result, Is.Null);
    }

    private static async Task VerifyManyToManyRelation<TParent, TChild, TGroup>(
        Func<TParent, TChild> createChild,
        Func<TGroup, List<TChild>> getChildren,
        Action<TGroup, List<TChild>> setChildren)
        where TParent : class, IStorable, new()
        where TChild : class, IStorable, new()
        where TGroup : class, IStorable, new()
    {
        var parentManager = GenericDM.Get<TParent>();
        var childManager = GenericDM.Get<TChild>();
        var groupManager = GenericDM.Get<TGroup>();
        var deleteGroups = await groupManager.CreateDelete<TGroup>()
            .Where(item => item.Id > 0)
            .RunWithError();
        var deleteChildren = await childManager.CreateDelete<TChild>()
            .Where(item => item.Id > 0)
            .RunWithError();
        var deleteParents = await parentManager.CreateDelete<TParent>()
            .Where(item => item.Id > 0)
            .RunWithError();
        Assert.That(deleteGroups.Success, Is.True,
            string.Join(Environment.NewLine, deleteGroups.Errors.Select(error => error.Message)));
        Assert.That(deleteChildren.Success, Is.True,
            string.Join(Environment.NewLine, deleteChildren.Errors.Select(error => error.Message)));
        Assert.That(deleteParents.Success, Is.True,
            string.Join(Environment.NewLine, deleteParents.Errors.Select(error => error.Message)));

        var parent = new TParent();
        typeof(TParent).GetProperty("Name")!.SetValue(parent, "N-N parent");
        var parentCreation = await parentManager.CreateWithError(parent);
        Assert.That(parentCreation.Success, Is.True,
            string.Join(Environment.NewLine, parentCreation.Errors.Select(error => error.Message)));

        var firstChild = createChild(parent);
        var secondChild = createChild(parent);
        typeof(TChild).GetProperty("Name")!.SetValue(firstChild, "N-N child one");
        typeof(TChild).GetProperty("Name")!.SetValue(secondChild, "N-N child two");
        var childCreation = await childManager.CreateWithError([firstChild, secondChild]);
        Assert.That(childCreation.Success, Is.True,
            string.Join(Environment.NewLine, childCreation.Errors.Select(error => error.Message)));

        var group = new TGroup();
        typeof(TGroup).GetProperty("Name")!.SetValue(group, "N-N group");
        setChildren(group, [firstChild, firstChild, secondChild]);
        var groupCreation = await groupManager.CreateWithError(group);
        var initiallyLoaded = await groupManager.CreateQuery<TGroup>()
            .Where(item => item.Id == group.Id)
            .SingleWithError();
        Assert.That(groupCreation.Success, Is.True,
            string.Join(Environment.NewLine, groupCreation.Errors.Select(error => error.Message)));
        Assert.That(initiallyLoaded.Success, Is.True,
            string.Join(Environment.NewLine, initiallyLoaded.Errors.Select(error => error.Message)));
        Assert.That(getChildren(initiallyLoaded.Result!).Select(item => item.Id),
            Is.EquivalentTo(new[] { firstChild.Id, secondChild.Id }),
            "Repeated items must create only one intermediate link.");

        setChildren(group, [secondChild]);
        var replacement = await groupManager.UpdateWithError(group);
        var afterReplacement = await groupManager.CreateQuery<TGroup>()
            .Where(item => item.Id == group.Id)
            .SingleWithError();
        Assert.That(replacement.Success, Is.True,
            string.Join(Environment.NewLine, replacement.Errors.Select(error => error.Message)));
        Assert.That(getChildren(afterReplacement.Result!).Select(item => item.Id),
            Is.EqualTo(new[] { secondChild.Id }));

        setChildren(group, []);
        var clearing = await groupManager.UpdateWithError(group);
        var afterClearing = await groupManager.CreateQuery<TGroup>()
            .Where(item => item.Id == group.Id)
            .SingleWithError();
        Assert.That(clearing.Success, Is.True,
            string.Join(Environment.NewLine, clearing.Errors.Select(error => error.Message)));
        Assert.That(getChildren(afterClearing.Result!), Is.Empty);

        setChildren(group, [firstChild]);
        var relink = await groupManager.UpdateWithError(group);
        var invalidChild = new TChild { Id = 999_999 };
        setChildren(group, [invalidChild]);
        var invalidUpdate = await groupManager.UpdateWithError(group);
        var afterInvalidUpdate = await groupManager.CreateQuery<TGroup>()
            .Where(item => item.Id == group.Id)
            .SingleWithError();
        Assert.That(relink.Success, Is.True,
            string.Join(Environment.NewLine, relink.Errors.Select(error => error.Message)));
        Assert.That(invalidUpdate.Success, Is.False);
        Assert.That(afterInvalidUpdate.Success, Is.True,
            string.Join(Environment.NewLine,
                afterInvalidUpdate.Errors.Select(error => error.Message)));
        Assert.That(getChildren(afterInvalidUpdate.Result!).Select(item => item.Id),
            Is.EqualTo(new[] { firstChild.Id }));
        Assert.That(getChildren(group), Has.Count.EqualTo(1));
        Assert.That(getChildren(group)[0], Is.SameAs(firstChild),
            "A failed N-N update must restore the caller's canonical collection.");

        var deletion = await groupManager.DeleteWithError(group);
        var firstChildAfterDeletion = await childManager.CreateQuery<TChild>()
            .Where(item => item.Id == firstChild.Id)
            .SingleWithError();
        Assert.That(deletion.Success, Is.True,
            string.Join(Environment.NewLine, deletion.Errors.Select(error => error.Message)));
        Assert.That(firstChildAfterDeletion.Success, Is.True,
            string.Join(Environment.NewLine,
                firstChildAfterDeletion.Errors.Select(error => error.Message)));
        Assert.That(firstChildAfterDeletion.Result, Is.Not.Null,
            "Deleting the N-N owner must keep linked items.");

        var invalidGroup = new TGroup();
        typeof(TGroup).GetProperty("Name")!.SetValue(invalidGroup, "Invalid N-N group");
        setChildren(invalidGroup, [invalidChild]);
        var invalidCreation = await groupManager.CreateWithError(invalidGroup);
        var invalidGroupAfterRollback = await groupManager
            .GetByIdWithError<TGroup>(invalidGroup.Id);
        Assert.That(invalidCreation.Success, Is.False);
        Assert.That(invalidGroupAfterRollback.Success, Is.False);
        Assert.That(invalidGroupAfterRollback.Result, Is.Null);
    }
}
