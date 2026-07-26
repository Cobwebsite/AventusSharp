using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataIncludeScopeTests
{
    [SetUp]
    public async Task SetUp()
    {
        var clear = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"included_scoped_holders\";" +
            "DELETE FROM \"included_scoped_records\";");
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        var visible = await IncludedScopedRecord.Create(new IncludedScopedRecord
        {
            Name = "Visible",
            IsVisible = true
        });
        var hidden = await IncludedScopedRecord.Create(new IncludedScopedRecord
        {
            Name = "Hidden",
            IsVisible = false
        });
        var manual = await IncludedScopedRecord.Create(new IncludedScopedRecord
        {
            Name = "Manual hidden",
            IsVisible = false
        });

        await IncludedScopedHolder.Create(new IncludedScopedHolder
        {
            Name = "Visible holder",
            Record = visible!
        });
        await IncludedScopedHolder.Create(new IncludedScopedHolder
        {
            Name = "Hidden holder",
            Record = hidden!
        });
        await IncludedScopedHolder.Create(new IncludedScopedHolder
        {
            Name = "Manual holder",
            Record = manual!
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"included_scoped_holders\";" +
            "DELETE FROM \"included_scoped_records\";");
    }

    [Test]
    public async Task Include_applies_the_declared_scope_of_the_related_model()
    {
        var result = await IncludedScopedHolder.StartQuery()
            .Include(item => item.Record)
            .Sort(item => item.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Visible holder" }));
    }

    [Test]
    public async Task IncludeWithoutScope_disables_the_related_models_declared_scope()
    {
        var result = await IncludedScopedHolder.StartQuery()
            .IncludeWithoutScope(item => item.Record)
            .Sort(item => item.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Hidden holder", "Manual holder", "Visible holder" }));
    }

    [Test]
    public async Task IncludeWithScope_replaces_the_related_models_declared_scope()
    {
        var result = await IncludedScopedHolder.StartQuery()
            .IncludeWithScope(item => item.Record, new NamedIncludedRecordScope())
            .Sort(item => item.Name, Sort.ASC)
            .RunWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.That(result.Result!.Select(item => item.Name),
            Is.EqualTo(new[] { "Manual holder" }));
    }
}
