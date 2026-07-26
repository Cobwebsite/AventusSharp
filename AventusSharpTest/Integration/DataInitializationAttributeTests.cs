using AventusSharp.Data.Manager;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataInitializationAttributeTests
{
    [SetUp]
    public async Task ClearDedicatedTable()
    {
        Assert.That(DedicatedTestStorage.Instance, Is.Not.Null);
        var result = await DedicatedTestStorage.Instance!.Execute(
            "DELETE FROM \"dedicated_storage_records\";");
        Assert.That(result.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(result.Errors));
    }

    [Test]
    public async Task Storage_attribute_creates_and_uses_a_dedicated_storage()
    {
        var creation = await DedicatedStorageRecord.CreateWithError(
            new DedicatedStorageRecord { Name = "Dedicated" });
        var dedicatedRows = await DedicatedTestStorage.Instance!.Query(
            "SELECT \"Name\" FROM \"dedicated_storage_records\";");
        var defaultTable = await IntegrationEnvironment.Storage.Query(
            "SELECT name FROM sqlite_master " +
            "WHERE type = 'table' AND name = 'dedicated_storage_records';");

        Assert.That(creation.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(creation.Errors));
        Assert.That(dedicatedRows.Result!.Single()["Name"], Is.EqualTo("Dedicated"));
        Assert.That(defaultTable.Result, Is.Empty);
    }

    [Test]
    public void Storage_attribute_reuses_the_registered_storage_instance()
    {
        var firstManager = (DedicatedStorageRecordManager)GenericDM.Get<DedicatedStorageRecord>();
        var secondManager = (DedicatedStorageRecordManager)GenericDM.Get<DedicatedStorageRecord>();

        Assert.That(firstManager, Is.SameAs(secondManager));
        Assert.That(firstManager.Storage, Is.SameAs(DedicatedTestStorage.Instance));
    }

    [Test]
    public async Task ManualInit_model_is_excluded_from_automatic_initialization()
    {
        var table = await IntegrationEnvironment.Storage.Query(
            "SELECT name FROM sqlite_master " +
            "WHERE type = 'table' AND name = 'manual_init_records';");

        Assert.That(table.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(table.Errors));
        Assert.That(table.Result, Is.Empty);
    }
}
