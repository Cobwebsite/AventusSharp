using AventusSharp.Data;
using AventusSharp.Data.Exporter;
using AventusSharp.Data.Importer;
using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;
using System.Globalization;

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
            "First line\nSecond line",
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
            "Name,Quantity\nValid,1\nInvalid,not-a-number\n");

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
        await File.WriteAllTextAsync(_csvPath, "Name,Quantity\n");

        var import = await CSVImporter.Import<TestCsvItem>(_csvPath);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(item => item.Id > 0);

        Assert.That(import.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(import.Errors));
        Assert.That(loaded.Result, Is.Empty);
    }

    [Test]
    public async Task Custom_mapper_renames_columns_for_export_and_import()
    {
        var exportConfig = new CSVExporterConfig<TestCsvItem>
        {
            mapper = mapper =>
            {
                mapper.Map(item => item.Name, "Label");
                mapper.Map(item => item.Quantity, "Amount");
                mapper.Ignore(item => item.Id);
            }
        };
        var importConfig = new CSVImporterConfig<TestCsvItem>
        {
            mapper = mapper =>
            {
                mapper.Map(item => item.Name, "Label");
                mapper.Map(item => item.Quantity, "Amount");
            }
        };

        var export = CSVExporter.Export(
            new List<TestCsvItem>
            {
                new() { Id = 91, Name = "Mapped", Quantity = 7 }
            },
            _csvPath,
            exportConfig);
        string csv = await File.ReadAllTextAsync(_csvPath);
        var import = await CSVImporter.Import(_csvPath, importConfig);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(item => item.Name == "Mapped");

        Assert.Multiple(() =>
        {
            Assert.That(export.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(export.Errors));
            Assert.That(csv, Does.StartWith("Label,Amount"));
            Assert.That(csv, Does.Not.Contain("Id"));
            Assert.That(import.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(import.Errors));
            Assert.That(loaded.Result, Has.Count.EqualTo(1));
            Assert.That(loaded.Result![0].Quantity, Is.EqualTo(7));
            Assert.That(loaded.Result[0].Id, Is.Not.EqualTo(91));
        });
    }

    [Test]
    public void Append_writes_the_header_only_once()
    {
        var first = CSVExporter.Export(
            new List<TestCsvItem>
            {
                new() { Name = "First", Quantity = 1 }
            },
            _csvPath);
        var second = CSVExporter.Export(
            new List<TestCsvItem>
            {
                new() { Name = "Second", Quantity = 2 }
            },
            _csvPath,
            new CSVExporterConfig<TestCsvItem> { Append = true });

        string[] lines = File.ReadAllLines(_csvPath);

        Assert.Multiple(() =>
        {
            Assert.That(first.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(first.Errors));
            Assert.That(second.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(second.Errors));
            Assert.That(lines.Count(line => line.Contains("Name")), Is.EqualTo(1));
            Assert.That(lines, Has.Some.Contains("First"));
            Assert.That(lines, Has.Some.Contains("Second"));
        });
    }

    [Test]
    public async Task ExportAll_paginates_without_missing_or_duplicating_records()
    {
        const int recordCount = 7;
        await TestCsvItem.BulkCreate(
            Enumerable.Range(1, recordCount)
                .Select(index => new TestCsvItem
                {
                    Name = $"Buffered-{index}",
                    Quantity = index
                })
                .ToList());
        var config = new CSVExporterConfig<TestCsvItem>(CultureInfo.InvariantCulture)
        {
            BufferSize = 3
        };

        var export = await CSVExporter.ExportAll(_csvPath, config);
        string[] dataLines = File.ReadAllLines(_csvPath).Skip(1).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(export.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(export.Errors));
            Assert.That(dataLines, Has.Length.EqualTo(recordCount));
            foreach (int index in Enumerable.Range(1, recordCount))
            {
                Assert.That(
                    dataLines.Count(line => line.Contains($"Buffered-{index},")),
                    Is.EqualTo(1),
                    $"Buffered-{index} must be exported exactly once.");
            }
        });
    }

    [Test]
    public async Task Import_with_id_preserves_the_csv_identifier()
    {
        const int expectedId = 900001;
        await File.WriteAllTextAsync(
            _csvPath,
            $"Id,Name,Quantity\n{expectedId},Preserved,4\n");
        var config = new CSVImporterConfig<TestCsvItem>
        {
            WithId = true
        };

        var import = await CSVImporter.Import(_csvPath, config);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(item => item.Id == expectedId);

        Assert.Multiple(() =>
        {
            Assert.That(import.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(import.Errors));
            Assert.That(loaded.Success, Is.True,
                IntegrationEnvironment.ErrorMessages(loaded.Errors));
            Assert.That(loaded.Result, Has.Count.EqualTo(1));
            Assert.That(loaded.Result![0].Name, Is.EqualTo("Preserved"));
            Assert.That(loaded.Result[0].Quantity, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Invalid_mapper_member_is_reported_and_import_is_rolled_back()
    {
        await File.WriteAllTextAsync(
            _csvPath,
            "Name,Quantity\nShouldNotPersist,5\n");
        var config = new CSVImporterConfig<TestCsvItem>
        {
            mapper = mapper =>
            {
                mapper.Map(item => item.Name, "Name");
                mapper.Map(item => item.Quantity, "Quantity");
                mapper.Ignore("UnknownMember");
            }
        };

        var import = await CSVImporter.Import(_csvPath, config);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(
                item => item.Name == "ShouldNotPersist");

        Assert.Multiple(() =>
        {
            Assert.That(import.Success, Is.False);
            Assert.That(import.Errors.Select(error => error.Code),
                Has.Some.EqualTo((int)DataErrorCode.MemberNotFound));
            Assert.That(loaded.Result, Is.Empty);
        });
    }

    [Test]
    public async Task Invalid_row_in_a_later_buffer_rolls_back_earlier_buffers()
    {
        await File.WriteAllTextAsync(
            _csvPath,
            "Name,Quantity\nFirst,1\nSecond,2\nInvalid,not-a-number\n");
        var config = new CSVImporterConfig<TestCsvItem>
        {
            BufferSize = 2
        };

        var import = await CSVImporter.Import(_csvPath, config);
        var loaded = await ((TestCsvItemManager)GenericDM.Get<TestCsvItem>())
            .WhereWithErrorNoCache<TestCsvItem>(item => item.Id > 0);

        Assert.Multiple(() =>
        {
            Assert.That(import.Success, Is.False);
            Assert.That(import.Errors, Is.Not.Empty);
            Assert.That(loaded.Result, Is.Empty,
                "The first flushed buffer must participate in the same transaction.");
        });
    }
}
