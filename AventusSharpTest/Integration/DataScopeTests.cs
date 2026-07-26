using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataScopeTests
{
    [SetUp]
    public async Task SetUp()
    {
        var reset = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"scoped_records\";" +
            "INSERT INTO \"scoped_records\" (\"Name\", \"Value\", \"IsVisible\") VALUES " +
            "('Visible low', 10, 1)," +
            "('Visible high', 70, 1)," +
            "('Hidden high', 90, 0);");
        Assert.That(reset.Success, Is.True, IntegrationEnvironment.ErrorMessages(reset.Errors));
    }

    [Test]
    public async Task Declared_scope_is_applied_to_queries_by_default()
    {
        var result = await ScopedRecord.StartQuery()
            .Sort(item => item.Value, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Visible low", "Visible high" }));
    }

    [Test]
    public async Task WithoutScope_returns_rows_hidden_by_the_declared_scope()
    {
        var result = await ScopedRecord.StartQuery()
            .WithoutScope()
            .Sort(item => item.Value, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Visible low", "Visible high", "Hidden high" }));
    }

    [Test]
    public async Task Manual_scope_replaces_the_declared_scope()
    {
        var result = await ScopedRecord.StartQuery()
            .WithScope<HighValueScopedRecord>()
            .Sort(item => item.Value, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Visible high", "Hidden high" }));
    }

    [Test]
    public async Task Update_builder_never_updates_rows_outside_the_active_scope()
    {
        var update = await ScopedRecord.StartUpdate()
            .Field(item => item.Value)
            .Where(item => item.Id > 0)
            .RunWithError(new ScopedRecord { Value = 42 });
        var rows = await ScopedRecord.StartQuery()
            .WithoutScope()
            .Sort(item => item.Name, Sort.ASC)
            .RunWithError();

        Assert.That(update.Success, Is.True, IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(rows.Success, Is.True, IntegrationEnvironment.ErrorMessages(rows.Errors));
        Assert.That(rows.Result!.Single(item => item.Name == "Visible low").Value, Is.EqualTo(42));
        Assert.That(rows.Result!.Single(item => item.Name == "Visible high").Value, Is.EqualTo(42));
        Assert.That(rows.Result!.Single(item => item.Name == "Hidden high").Value, Is.EqualTo(90));
    }

    [Test]
    public async Task Delete_builder_never_deletes_rows_outside_the_active_scope()
    {
        var deletion = await ScopedRecord.StartDelete()
            .Where(item => item.Id > 0)
            .RunWithError();
        var remaining = await ScopedRecord.StartQuery()
            .WithoutScope()
            .RunWithError();

        Assert.That(deletion.Success, Is.True, IntegrationEnvironment.ErrorMessages(deletion.Errors));
        Assert.That(remaining.Success, Is.True, IntegrationEnvironment.ErrorMessages(remaining.Errors));
        Assert.That(remaining.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Hidden high" }));
    }

    [Test]
    public async Task Scope_is_anded_with_the_complete_Where_OrWhere_group()
    {
        var result = await ScopedRecord.StartQuery()
            .Where(item => item.Value == 10)
            .OrWhere(item => item.Value == 90)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Visible low" }),
            "The scope must apply to both branches of the user filter.");
    }
}
