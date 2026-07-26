using AventusSharp.Data.CustomTableMembers;
using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataSpecializedTypeTests
{
    [SetUp]
    public async Task ClearTable()
    {
        var result = await IntegrationEnvironment.Storage.Execute(
            "DELETE FROM \"test_specialized_data\";");
        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task StorableLists_round_trip_through_the_database()
    {
        var item = new TestSpecializedData
        {
            Numbers = new StorableListInt { 1, -2, 30 },
            ShortNumbers = new StorableListShort { short.MinValue, 0, short.MaxValue },
            LongNumbers = new StorableListLong { long.MinValue, 0, long.MaxValue },
            FloatNumbers = new StorableListFloat { -1.5f, 0, 42.25f },
            DoubleNumbers = new StorableListDouble { -1.5d, 0, 42.25d },
            Flags = new StorableListBool { true, false, true },
            Labels = new StorableListString { "alpha", "beta" }
        };

        var creation = await TestSpecializedData.CreateWithError(item);
        var loaded = await ((TestSpecializedDataManager)GenericDM.Get<TestSpecializedData>())
            .GetByIdWithErrorNoCache<TestSpecializedData>(item.Id);

        Assert.That(creation.Success, Is.True, IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Result!.Numbers, Is.EqualTo(new[] { 1, -2, 30 }));
            Assert.That(loaded.Result.ShortNumbers,
                Is.EqualTo(new[] { short.MinValue, (short)0, short.MaxValue }));
            Assert.That(loaded.Result.LongNumbers,
                Is.EqualTo(new[] { long.MinValue, 0, long.MaxValue }));
            Assert.That(loaded.Result.FloatNumbers,
                Is.EqualTo(new[] { -1.5f, 0, 42.25f }));
            Assert.That(loaded.Result.DoubleNumbers,
                Is.EqualTo(new[] { -1.5d, 0, 42.25d }));
            Assert.That(loaded.Result.Flags, Is.EqualTo(new[] { true, false, true }));
            Assert.That(loaded.Result.Labels, Is.EqualTo(new[] { "alpha", "beta" }));
        });
    }

    [Test]
    public async Task Empty_StorableLists_are_loaded_as_empty_lists()
    {
        var item = await TestSpecializedData.Create(new TestSpecializedData());
        var loaded = await ((TestSpecializedDataManager)GenericDM.Get<TestSpecializedData>())
            .GetByIdWithErrorNoCache<TestSpecializedData>(item!.Id);

        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Numbers, Is.Empty);
        Assert.That(loaded.Result.ShortNumbers, Is.Empty);
        Assert.That(loaded.Result.LongNumbers, Is.Empty);
        Assert.That(loaded.Result.FloatNumbers, Is.Empty);
        Assert.That(loaded.Result.DoubleNumbers, Is.Empty);
        Assert.That(loaded.Result.Flags, Is.Empty);
        Assert.That(loaded.Result.Labels, Is.Empty);
    }

    [Test]
    public async Task AventusFile_uri_round_trips_as_a_custom_table_member()
    {
        var item = new TestSpecializedData
        {
            Document = new TestDocumentFile { Uri = "/documents/manual.pdf" }
        };

        var creation = await TestSpecializedData.CreateWithError(item);
        var loaded = await ((TestSpecializedDataManager)GenericDM.Get<TestSpecializedData>())
            .GetByIdWithErrorNoCache<TestSpecializedData>(item.Id);

        Assert.That(creation.Success, Is.True, IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(loaded.Success, Is.True, IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Document, Is.TypeOf<TestDocumentFile>());
        Assert.That(loaded.Result.Document.Uri, Is.EqualTo("/documents/manual.pdf"));
        Assert.That(loaded.Result.Document.Upload, Is.Null);
    }

    [Test]
    public async Task StorableLists_can_be_replaced_by_an_update()
    {
        var item = await TestSpecializedData.Create(new TestSpecializedData
        {
            Numbers = new StorableListInt { 1 },
            Labels = new StorableListString { "before" }
        });
        item!.Numbers = new StorableListInt { 2, 3 };
        item.Labels = new StorableListString { "after", "updated" };

        var update = await TestSpecializedData.UpdateWithError(item);
        var loaded = await ((TestSpecializedDataManager)GenericDM.Get<TestSpecializedData>())
            .GetByIdWithErrorNoCache<TestSpecializedData>(item.Id);

        Assert.That(update.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(loaded.Result!.Numbers, Is.EqualTo(new[] { 2, 3 }));
        Assert.That(loaded.Result.Labels, Is.EqualTo(new[] { "after", "updated" }));
    }

    [Test]
    public async Task String_list_preserves_values_containing_commas()
    {
        var item = await TestSpecializedData.Create(new TestSpecializedData
        {
            Labels = new StorableListString
            {
                "alpha,beta",
                "quoted \"value\"",
                "",
                "Éclairage 日本語"
            }
        });
        var loaded = await ((TestSpecializedDataManager)GenericDM.Get<TestSpecializedData>())
            .GetByIdWithErrorNoCache<TestSpecializedData>(item!.Id);

        Assert.That(loaded.Result!.Labels,
            Is.EqualTo(new[]
            {
                "alpha,beta",
                "quoted \"value\"",
                "",
                "Éclairage 日本語"
            }));
    }

    [Test]
    public async Task AventusFile_uri_can_be_updated_and_preserves_special_characters()
    {
        var item = await TestSpecializedData.Create(new TestSpecializedData
        {
            Document = new TestDocumentFile { Uri = "/documents/initial.pdf" }
        });
        item!.Document = new TestDocumentFile
        {
            Uri = "/documents/été/O'Reilly manual (final).pdf?version=2"
        };

        var update = await TestSpecializedData.UpdateWithError(item);
        var loaded = await ((TestSpecializedDataManager)GenericDM.Get<TestSpecializedData>())
            .GetByIdWithErrorNoCache<TestSpecializedData>(item.Id);

        Assert.That(update.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(update.Errors));
        Assert.That(loaded.Result!.Document.Uri,
            Is.EqualTo("/documents/été/O'Reilly manual (final).pdf?version=2"));
        Assert.That(loaded.Result.Document.Upload, Is.Null);
    }

    [Test]
    public async Task Empty_AventusFile_uri_round_trips_as_an_empty_file_value()
    {
        var item = await TestSpecializedData.Create(new TestSpecializedData
        {
            Document = new TestDocumentFile { Uri = "" }
        });
        var loaded = await ((TestSpecializedDataManager)GenericDM.Get<TestSpecializedData>())
            .GetByIdWithErrorNoCache<TestSpecializedData>(item!.Id);

        Assert.That(loaded.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(loaded.Errors));
        Assert.That(loaded.Result!.Document, Is.Not.Null);
        Assert.That(loaded.Result.Document.Uri, Is.Empty);
    }

    [Test]
    public async Task Null_AventusFile_is_rejected_for_a_required_column()
    {
        var item = new TestSpecializedData { Document = null! };

        var creation = await TestSpecializedData.CreateWithError(item);

        Assert.That(creation.Success, Is.False);
        Assert.That(creation.Errors, Is.Not.Empty);
    }
}
