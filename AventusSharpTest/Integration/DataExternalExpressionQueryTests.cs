using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Hosting;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;
using System.Linq.Expressions;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataExternalExpressionQueryTests
{
    [SetUp]
    public async Task SetUp()
    {
        var clear = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_sensors\";DELETE FROM \"test_lamps\";DELETE FROM \"test_rooms\";" +
            "DELETE FROM \"test_auto_projection_wrappers\";DELETE FROM \"test_auto_projection_children\";" +
            "DELETE FROM \"test_auto_projection_parents\";" +
            "DELETE FROM \"test_projection_wrappers\";DELETE FROM \"test_projection_children\";" +
            "DELETE FROM \"test_projection_parents\";");
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        var first = await TestRoom.Create(new TestRoom { Name = "First", Code = "external-first" });
        var second = await TestRoom.Create(new TestRoom { Name = "Second", Code = "external-second" });
        var empty = await TestRoom.Create(new TestRoom { Name = "Empty", Code = "external-empty" });
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        Assert.That(empty, Is.Not.Null);

        await TestSensor.Create(new TestSensor { Name = "Hot", Room = first });
        await TestSensor.Create(new TestSensor { Name = "Cold", Room = first });
        await TestSensor.Create(new TestSensor { Name = "Warm", Room = second });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task External_predicate_still_works_when_its_field_is_ignored(bool whereFirst)
    {
        var parent = (await TestProjectionParent.Create(
            new TestProjectionParent { Name = "Selected" }))!;
        var child = (await TestProjectionChild.Create(new TestProjectionChild
        {
            Name = "Filter", Detail = "Visible", Parent = parent
        }))!;

        ((IDatabaseDM)GenericDM.Get<TestProjectionChild>())
            .RemoveRecordsItems<TestProjectionChild>([child.Id]);
        ((IDatabaseDM)GenericDM.Get<TestProjectionParent>())
            .RemoveRecordsItems<TestProjectionParent>([parent.Id]);

        var query = TestProjectionParent.StartQuery();
        if (whereFirst)
            query.Where(item => item.Child!.Name == "Filter")
                .Ignore(item => item.Child!.Name);
        else
            query.Ignore(item => item.Child!.Name)
                .Where(item => item.Child!.Name == "Filter");
        var result = await query.RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result![0].Id, Is.EqualTo(parent.Id));
            Assert.That(result.Result[0].Child!.Name, Is.Empty);
            Assert.That(result.Result[0].Child!.Detail, Is.EqualTo("Visible"));
        });
    }

    [Test]
    public async Task External_predicate_does_not_clear_ignored_field_on_cached_child()
    {
        var parent = (await TestProjectionParent.Create(
            new TestProjectionParent { Name = "Cached" }))!;
        var child = (await TestProjectionChild.Create(new TestProjectionChild
        {
            Name = "Filter", Detail = "Visible", Parent = parent
        }))!;

        var query = TestProjectionParent.StartQuery()
            .Ignore(item => item.Child!.Name)
            .Where(item => item.Child!.Name == "Filter");
        var first = await query.RunWithError();
        var second = await query.RunWithError();

        Assert.That(first.Success && second.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(first.Errors.Concat(second.Errors)));
        Assert.Multiple(() =>
        {
            Assert.That(first.Result, Has.Count.EqualTo(1));
            Assert.That(second.Result, Has.Count.EqualTo(1));
            Assert.That(first.Result![0].Child!.Id, Is.EqualTo(child.Id));
            Assert.That(second.Result![0].Child!.Name, Is.EqualTo("Filter"));
            Assert.That(child.Name, Is.EqualTo("Filter"));
        });
    }

    [Test]
    public async Task Nested_external_predicate_respects_ignored_field()
    {
        var parent = (await TestProjectionParent.Create(
            new TestProjectionParent { Name = "Nested" }))!;
        var child = (await TestProjectionChild.Create(new TestProjectionChild
        {
            Name = "Filter", Detail = "Visible", Parent = parent
        }))!;
        var wrapper = (await TestProjectionWrapper.Create(
            new TestProjectionWrapper { Parent = parent }))!;

        ((IDatabaseDM)GenericDM.Get<TestProjectionChild>())
            .RemoveRecordsItems<TestProjectionChild>([child.Id]);
        ((IDatabaseDM)GenericDM.Get<TestProjectionParent>())
            .RemoveRecordsItems<TestProjectionParent>([parent.Id]);
        ((IDatabaseDM)GenericDM.Get<TestProjectionWrapper>())
            .RemoveRecordsItems<TestProjectionWrapper>([wrapper.Id]);

        var result = await TestProjectionWrapper.StartQuery()
            .Ignore(item => item.Parent.Child!.Name)
            .Where(item => item.Parent.Child!.Name == "Filter")
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result![0].Id, Is.EqualTo(wrapper.Id));
            Assert.That(result.Result[0].Parent.Child!.Name, Is.Empty);
            Assert.That(result.Result[0].Parent.Child!.Detail, Is.EqualTo("Visible"));
        });
    }

    [Test]
    public async Task External_sort_and_group_respect_ignored_field()
    {
        var first = (await TestProjectionParent.Create(
            new TestProjectionParent { Name = "First" }))!;
        var second = (await TestProjectionParent.Create(
            new TestProjectionParent { Name = "Second" }))!;
        var firstChild = (await TestProjectionChild.Create(new TestProjectionChild
        {
            Name = "A", Detail = "First detail", Parent = first
        }))!;
        var secondChild = (await TestProjectionChild.Create(new TestProjectionChild
        {
            Name = "B", Detail = "Second detail", Parent = second
        }))!;

        ((IDatabaseDM)GenericDM.Get<TestProjectionChild>())
            .RemoveRecordsItems<TestProjectionChild>([firstChild.Id, secondChild.Id]);
        ((IDatabaseDM)GenericDM.Get<TestProjectionParent>())
            .RemoveRecordsItems<TestProjectionParent>([first.Id, second.Id]);

        var sorted = await TestProjectionParent.StartQuery()
            .Ignore(item => item.Child!.Name)
            .Sort(item => item.Child!.Name, Sort.DESC)
            .RunWithError();
        var grouped = await TestProjectionParent.StartQuery()
            .Ignore(item => item.Child!.Name)
            .Group(item => item.Child!.Name)
            .Sort(item => item.Child!.Name, Sort.ASC)
            .RunWithError();

        Assert.That(sorted.Success && grouped.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(sorted.Errors.Concat(grouped.Errors)));
        Assert.Multiple(() =>
        {
            Assert.That(sorted.Result!.Select(item => item.Name),
                Is.EqualTo(new[] { "Second", "First" }));
            Assert.That(sorted.Result!.All(item => item.Child!.Name == ""), Is.True);
            Assert.That(grouped.Result!.Select(item => item.Name),
                Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(grouped.Result!.All(item => item.Child!.Name == ""), Is.True);
        });
    }

    [Test]
    public async Task External_collection_predicate_respects_ignored_relation()
    {
        var result = await TestRoom.StartQuery()
            .Ignore(room => room.Sensors)
            .Where(room => room.Sensors.Any(sensor => sensor.Name == "Hot"))
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result![0].Name, Is.EqualTo("First"));
            Assert.That(result.Result[0].Sensors, Is.Empty);
        });
    }

    [Test]
    public async Task External_scope_respects_ignored_collection()
    {
        var result = await TestRoom.StartQuery()
            .Ignore(room => room.Sensors)
            .WithScope<RoomWithSensorsScope>()
            .Sort(room => room.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(room => room.Name),
            Is.EqualTo(new[] { "First", "Second" }));
        Assert.That(result.Result!.All(room => room.Sensors.Count == 0), Is.True);
    }

    [Test]
    public async Task AutoRead_retains_canonical_value_of_ignored_nested_field()
    {
        var parentCreation = await TestAutoProjectionParent.CreateWithError(
            new TestAutoProjectionParent { Name = "Parent" });
        Assert.That(parentCreation.Success, Is.True, IntegrationEnvironment.ErrorMessages(parentCreation.Errors));
        var parent = parentCreation.Result!;
        var childCreation = await TestAutoProjectionChild.CreateWithError(new TestAutoProjectionChild
        {
            Name = "Filter", Detail = "Visible", Parent = parent
        });
        Assert.That(childCreation.Success, Is.True, IntegrationEnvironment.ErrorMessages(childCreation.Errors));
        var child = childCreation.Result!;
        var wrapperCreation = await TestAutoProjectionWrapper.CreateWithError(new TestAutoProjectionWrapper
        {
            Parent = parent
        });
        Assert.That(wrapperCreation.Success, Is.True, IntegrationEnvironment.ErrorMessages(wrapperCreation.Errors));
        var wrapper = wrapperCreation.Result!;

        ((IDatabaseDM)GenericDM.Get<TestAutoProjectionChild>())
            .RemoveRecordsItems<TestAutoProjectionChild>([child.Id]);
        ((IDatabaseDM)GenericDM.Get<TestAutoProjectionParent>())
            .RemoveRecordsItems<TestAutoProjectionParent>([parent.Id]);
        ((IDatabaseDM)GenericDM.Get<TestAutoProjectionWrapper>())
            .RemoveRecordsItems<TestAutoProjectionWrapper>([wrapper.Id]);

        var result = await TestAutoProjectionWrapper.StartQuery()
            .Ignore(item => item.Parent.Child!.Name)
            .Where(item => item.Parent.Child!.Name == "Filter")
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        var canonical = await GenericDM.Get<TestAutoProjectionChild>()
            .GetByIdWithError<TestAutoProjectionChild>(child.Id);
        Assert.That(canonical.Success, Is.True, IntegrationEnvironment.ErrorMessages(canonical.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result![0].Parent.Child, Is.SameAs(canonical.Result));
            Assert.That(result.Result[0].Parent.Child!.Name, Is.EqualTo("Filter"));
            Assert.That(result.Result[0].Parent.Child!.Detail, Is.EqualTo("Visible"));
        });
    }

    [Test]
    public async Task External_predicate_combines_with_local_or_before_paging()
    {
        var result = await TestRoom.StartQuery()
            .Where(room => room.Sensors.Any(sensor => sensor.Name == "Hot"))
            .OrWhere(room => room.Name == "Empty")
            .Sort(room => room.Name, Sort.ASC)
            .Offset(1)
            .Take(1)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(room => room.Name), Is.EqualTo(new[] { "First" }));
    }

    [Test]
    public async Task External_sort_and_group_run_before_paging()
    {
        var sorted = await TestRoom.StartQuery()
            .Sort(room => room.Sensors.Count, Sort.DESC)
            .Sort(room => room.Name, Sort.ASC)
            .Take(2)
            .RunWithError();
        var grouped = await TestRoom.StartQuery()
            .Group(room => room.Sensors.Count)
            .Sort(room => room.Sensors.Count, Sort.ASC)
            .RunWithError();

        Assert.That(sorted.Success, Is.True, IntegrationEnvironment.ErrorMessages(sorted.Errors));
        Assert.That(sorted.Result!.Select(room => room.Name), Is.EqualTo(new[] { "First", "Second" }));
        Assert.That(grouped.Success, Is.True, IntegrationEnvironment.ErrorMessages(grouped.Errors));
        Assert.That(grouped.Result, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Negated_external_collection_predicate_matches_empty_relation()
    {
        var result = await TestRoom.StartQuery()
            .Where(room => !room.Sensors.Any())
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(room => room.Name), Is.EqualTo(new[] { "Empty" }));
    }

    [Test]
    public async Task Nullable_reverse_link_and_nested_path_can_be_filtered()
    {
        var parentWithChild = await TestProjectionParent.Create(new TestProjectionParent { Name = "With child" });
        var parentWithoutChild = await TestProjectionParent.Create(new TestProjectionParent { Name = "Without child" });
        Assert.That(parentWithChild, Is.Not.Null);
        Assert.That(parentWithoutChild, Is.Not.Null);
        await TestProjectionChild.Create(new TestProjectionChild
        {
            Name = "Target",
            Detail = "Detail",
            Parent = parentWithChild!
        });
        await TestProjectionWrapper.Create(new TestProjectionWrapper { Parent = parentWithChild! });
        await TestProjectionWrapper.Create(new TestProjectionWrapper { Parent = parentWithoutChild! });

        var parents = await TestProjectionParent.StartQuery()
            .Where(parent => parent.Child!.Name == "Target")
            .RunWithError();
        var wrappers = await TestProjectionWrapper.StartQuery()
            .Where(wrapper => wrapper.Parent.Child!.Name == "Target")
            .RunWithError();
        var sorted = await TestProjectionParent.StartQuery()
            .Sort(parent => parent.Child!.Name, Sort.ASC)
            .RunWithError();
        var grouped = await TestProjectionParent.StartQuery()
            .Group(parent => parent.Child!.Name)
            .RunWithError();

        Assert.That(parents.Success, Is.True, IntegrationEnvironment.ErrorMessages(parents.Errors));
        Assert.That(parents.Result!.Select(parent => parent.Name), Is.EqualTo(new[] { "With child" }));
        Assert.That(wrappers.Success, Is.True, IntegrationEnvironment.ErrorMessages(wrappers.Errors));
        Assert.That(wrappers.Result, Has.Count.EqualTo(1));
        Assert.That(sorted.Success, Is.True, IntegrationEnvironment.ErrorMessages(sorted.Errors));
        Assert.That(sorted.Result!.Select(parent => parent.Name),
            Is.EqualTo(new[] { "Without child", "With child" }));
        Assert.That(grouped.Success, Is.True, IntegrationEnvironment.ErrorMessages(grouped.Errors));
        Assert.That(grouped.Result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task External_scope_is_applied_before_limit()
    {
        var result = await TestRoom.StartQuery()
            .WithScope<RoomWithSensorsScope>()
            .Sort(room => room.Name, Sort.ASC)
            .Take(1)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(room => room.Name), Is.EqualTo(new[] { "First" }));
    }

    [Test]
    public async Task Prepared_external_predicate_uses_each_execution_value()
    {
        string target = "";
        var prepared = TestRoom.StartQuery()
            .WhereWithParameters(room => room.Sensors.Any(sensor => sensor.Name == target));
        var hot = await prepared.New()
            .SetVariables(set => set("target", "Hot"))
            .RunWithError();
        var warm = await prepared.New()
            .Prepare("Warm")
            .RunWithError();

        Assert.That(hot.Success, Is.True, IntegrationEnvironment.ErrorMessages(hot.Errors));
        Assert.That(hot.Result!.Select(room => room.Name), Is.EqualTo(new[] { "First" }));
        Assert.That(warm.Success, Is.True, IntegrationEnvironment.ErrorMessages(warm.Errors));
        Assert.That(warm.Result!.Select(room => room.Name), Is.EqualTo(new[] { "Second" }));
    }

    [Test]
    public async Task Prepared_external_predicate_requires_a_runtime_value()
    {
        string target = "";
        var prepared = TestRoom.StartQuery()
            .WhereWithParameters(room => room.Sensors.Any(sensor => sensor.Name == target));
        var result = await prepared.New().RunWithError();

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task External_predicate_streams_the_filtered_page()
    {
        var names = new List<string>();
        var result = await TestRoom.StartQuery()
            .Where(room => room.Sensors.Any())
            .Sort(room => room.Name, Sort.ASC)
            .Take(1)
            .RunStreamWithError(room =>
            {
                names.Add(room.Name);
                return Task.FromResult(new AventusSharp.Tools.VoidWithError());
            });

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(names, Is.EqualTo(new[] { "First" }));
    }

    [Test]
    public async Task Existence_checks_support_external_predicates()
    {
        var hot = await TestRoom.StartExist()
            .Where(room => room.Sensors.Any(sensor => sensor.Name == "Hot"))
            .RunWithError();
        string target = "";
        var prepared = TestRoom.StartExist()
            .WhereWithParameters(room => room.Sensors.Any(sensor => sensor.Name == target));
        var missing = await prepared.New()
            .SetVariables(set => set("target", "Missing"))
            .RunWithError();

        Assert.That(hot.Success, Is.True, IntegrationEnvironment.ErrorMessages(hot.Errors));
        Assert.That(hot.Result, Is.True);
        Assert.That(missing.Success, Is.True, IntegrationEnvironment.ErrorMessages(missing.Errors));
        Assert.That(missing.Result, Is.False);
    }

    [Test]
    public async Task Update_selects_only_rows_matching_external_predicate()
    {
        var result = await TestRoom.StartUpdate()
            .Field(room => room.Description)
            .Where(room => room.Sensors.Any(sensor => sensor.Name == "Hot"))
            .RunWithError(new TestRoom { Description = "Updated" });
        var rows = await TestRoom.StartQuery()
            .Sort(room => room.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(room => room.Description),
            Is.EqualTo(new[] { null, "Updated", null }));
    }

    [Test]
    public async Task Delete_selects_only_rows_matching_external_predicate()
    {
        var result = await TestRoom.StartDelete()
            .Where(room => room.Sensors.Any(sensor => sensor.Name == "Hot"))
            .RunWithError();
        var rows = await TestRoom.StartQuery()
            .Sort(room => room.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result, Has.Count.EqualTo(1));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(room => room.Name), Is.EqualTo(new[] { "Empty", "Second" }));
    }

    [Test]
    public async Task Prepared_update_refreshes_external_selection_each_run()
    {
        string target = "";
        var prepared = TestRoom.StartUpdate()
            .Field(room => room.Description)
            .WhereWithParameters(room => room.Sensors.Any(sensor => sensor.Name == target));
        var first = await prepared.New()
            .SetVariables(set => set("target", "Hot"))
            .RunWithError(new TestRoom { Description = "Hot room" });
        var second = await prepared.New()
            .SetVariables(set => set("target", "Warm"))
            .RunWithError(new TestRoom { Description = "Warm room" });
        var rows = await TestRoom.StartQuery()
            .Sort(room => room.Name, Sort.ASC)
            .RunWithError();

        Assert.That(first.Success, Is.True, IntegrationEnvironment.ErrorMessages(first.Errors));
        Assert.That(second.Success, Is.True, IntegrationEnvironment.ErrorMessages(second.Errors));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(room => room.Description),
            Is.EqualTo(new[] { null, "Hot room", "Warm room" }));
    }

    [Test]
    public async Task Prepared_delete_uses_external_selection()
    {
        string target = "";
        var prepared = TestRoom.StartDelete()
            .WhereWithParameters(room => room.Sensors.Any(sensor => sensor.Name == target));
        var result = await prepared.New()
            .SetVariables(set => set("target", "Warm"))
            .RunWithError();
        var rows = await TestRoom.StartQuery()
            .Sort(room => room.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Select(room => room.Name), Is.EqualTo(new[] { "Empty", "First" }));
    }

    [Test]
    public async Task Local_predicate_sort_and_limit_stay_in_sql()
    {
        var query = (DatabaseQueryBuilder<TestRoom>)TestRoom.StartQuery();
        query.Where(room => room.Name == "First");
        query.Sort(room => room.Name, Sort.ASC);
        query.Limit(1);
        var result = await query.RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(room => room.Name), Is.EqualTo(new[] { "First" }));
        Assert.That(query.RequiresPostProcessing, Is.False);
        Assert.That(query.RequiresPostSort, Is.False);
        Assert.That(query.SqlLimitSize, Is.EqualTo(1));
        Assert.That(query.info!.Sql, Does.Contain(" WHERE "));
        Assert.That(query.info.Sql, Does.Contain(" ORDER BY "));
        Assert.That(query.info.Sql, Does.Contain(" LIMIT 1"));
    }
}

public sealed class RoomWithSensorsScope : Scope<TestRoom>
{
    public override Expression<Func<TestRoom, bool>>? Where(IAventusContext? context) =>
        room => room.Sensors.Any();
}
