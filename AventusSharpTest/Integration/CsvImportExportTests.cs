using AventusSharp.Data.Exporter;
using AventusSharp.Data.Importer;
using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class CsvImportExportTests
{
    private string _csvPath = "";

    [SetUp]
    public async Task SetUp()
    {
        _csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"items-{Guid.NewGuid():N}.csv");
        var result = await IntegrationEnvironment.Storage.Execute("DELETE FROM \"test_csv_items\";");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_csvPath))
            File.Delete(_csvPath);
    }

    [Test]
    public async Task ExportAll_and_Import_round_trip_records()
    {
        await TestCsvItem.BulkCreate(new List<TestCsvItem>
        {
            new() { Name = "First", Quantity = 2 },
            new() { Name = "Second", Quantity = 5 }
        });

        var export = await CSVExporter.ExportAll<TestCsvItem>(_csvPath);
        Assert.That(export.Success, Is.True, IntegrationEnvironment.ErrorMessages(export.Errors));
        Assert.That(File.ReadAllText(_csvPath), Does.Contain("First").And.Contain("Second"));

        var clear = await IntegrationEnvironment.Storage.Execute("DELETE FROM \"test_csv_items\";");
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        var import = await CSVImporter.Import<TestCsvItem>(_csvPath);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(item => item.Quantity >= 2);

        Assert.That(import.Success, Is.True, IntegrationEnvironment.ErrorMessages(import.Errors));
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Select(item => item.Name), Is.EquivalentTo(new[] { "First", "Second" }));
    }

    [Test]
    public async Task Import_reports_a_missing_file()
    {
        var result = await CSVImporter.Import<TestCsvItem>(_csvPath);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Select(error => error.Message),
            Has.Some.Contains("can't be found"));
    }

    [Test]
    public async Task Round_trip_preserves_csv_metacharacters_and_unicode()
    {
        var expectedNames = new[]
        {
            "Comma, value",
            "Quoted \"value\"",
            "First line\r\nSecond line",
            "Éclairage 日本語"
        };
        await TestCsvItem.BulkCreate(expectedNames
            .Select((name, index) => new TestCsvItem { Name = name, Quantity = index + 1 })
            .ToList());

        var export = await CSVExporter.ExportAll<TestCsvItem>(_csvPath);
        var clear = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_csv_items\";");
        var import = await CSVImporter.Import<TestCsvItem>(_csvPath);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(item => item.Quantity > 0);

        Assert.That(export.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(export.Errors));
        Assert.That(clear.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(clear.Errors));
        Assert.That(import.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(import.Errors));
        Assert.That(loaded.Result!.OrderBy(item => item.Quantity).Select(item => item.Name),
            Is.EqualTo(expectedNames));
    }

    [Test]
    public async Task Invalid_field_conversion_reports_an_error_without_partial_import()
    {
        await File.WriteAllTextAsync(
            _csvPath,
            "Name,Quantity\r\nValid,1\r\nInvalid,not-a-number\r\n");

        var import = await CSVImporter.Import<TestCsvItem>(_csvPath);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(item => item.Id > 0);

        Assert.That(import.Success, Is.False);
        Assert.That(import.Errors, Is.Not.Empty);
        Assert.That(loaded.Result, Is.Empty);
    }

    [Test]
    public async Task Header_only_file_imports_successfully_without_rows()
    {
        await File.WriteAllTextAsync(_csvPath, "Name,Quantity\r\n");

        var import = await CSVImporter.Import<TestCsvItem>(_csvPath);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(item => item.Id > 0);

        Assert.That(import.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(import.Errors));
        Assert.That(loaded.Result, Is.Empty);
    }
}
