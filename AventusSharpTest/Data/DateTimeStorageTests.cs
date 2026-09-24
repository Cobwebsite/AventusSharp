using AventusSharp.Data;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using NUnit.Framework;

namespace AventusSharpTest.Data;

[TestFixture]
[NonParallelizable]
public class DateTimeStorageTests
{
    private DateTimeStorageMode _previousMode;

    private sealed class TemporalModel
    {
        public DateTime DefaultValue { get; set; }

        [UTC]
        public DateTime UtcValue { get; set; }

        [Local]
        public DateTime LocalValue { get; set; }

        public Datetime AventusValue { get; set; } = new();

        [UTC]
        public Datetime AventusUtcValue { get; set; } = new();

        [Local]
        public Datetime AventusLocalValue { get; set; } = new();
    }

    [SetUp]
    public void SetUp()
    {
        _previousMode = DataMainManager.Config.DateTimeStorageMode;
    }

    [TearDown]
    public void TearDown()
    {
        DataMainManager.Config.DateTimeStorageMode = _previousMode;
    }

    [Test]
    public void Global_mode_is_used_when_no_attribute_is_present()
    {
        DataMainManager.Config.DateTimeStorageMode = DateTimeStorageMode.Utc;
        TableMemberInfoSql member = GetMember(nameof(TemporalModel.DefaultValue));
        var input = DateTime.SpecifyKind(new DateTime(2026, 9, 3, 10, 0, 0), DateTimeKind.Local);
        var model = new TemporalModel { DefaultValue = input };

        var stored = (DateTime)member.GetValueToSave(model)!;
        member.ApplySqlValue(model, stored.ToString("O"));

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.EqualTo(input.ToUniversalTime()));
            Assert.That(stored.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(model.DefaultValue.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Attributes_override_the_global_mode()
    {
        DataMainManager.Config.DateTimeStorageMode = DateTimeStorageMode.Local;
        TableMemberInfoSql utcMember = GetMember(nameof(TemporalModel.UtcValue));
        DataMainManager.Config.DateTimeStorageMode = DateTimeStorageMode.Utc;
        TableMemberInfoSql localMember = GetMember(nameof(TemporalModel.LocalValue));
        var unspecified = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Unspecified);

        var utcStored = (DateTime)utcMember.TransformQueryValue(unspecified)!;
        var localStored = (DateTime)localMember.TransformQueryValue(utcStored)!;

        Assert.Multiple(() =>
        {
            Assert.That(utcStored, Is.EqualTo(
                DateTime.SpecifyKind(unspecified, DateTimeKind.Local).ToUniversalTime()));
            Assert.That(utcStored.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(localStored, Is.EqualTo(DateTime.SpecifyKind(
                utcStored.ToLocalTime(), DateTimeKind.Unspecified)));
            Assert.That(localStored.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    [Test]
    public void Local_is_the_default_configuration()
    {
        Assert.That(new DataManagerConfig().DateTimeStorageMode,
            Is.EqualTo(DateTimeStorageMode.Local));
    }

    [Test]
    public void Aventus_datetime_uses_the_same_typed_storage_value_as_system_datetime()
    {
        DataMainManager.Config.DateTimeStorageMode = DateTimeStorageMode.Utc;
        TableMemberInfoSql member = GetMember(nameof(TemporalModel.AventusValue));
        var input = DateTime.SpecifyKind(
            new DateTime(2026, 9, 3, 10, 20, 30),
            DateTimeKind.Local);
        var model = new TemporalModel { AventusValue = new Datetime(input) };

        var stored = member.GetValueToSave(model);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.TypeOf<DateTime>());
            Assert.That(stored, Is.EqualTo(input.ToUniversalTime()));
        });
    }

    [Test]
    public void Attributes_override_the_global_mode_for_aventus_datetime()
    {
        DataMainManager.Config.DateTimeStorageMode = DateTimeStorageMode.Local;
        TableMemberInfoSql utcMember = GetMember(nameof(TemporalModel.AventusUtcValue));
        DataMainManager.Config.DateTimeStorageMode = DateTimeStorageMode.Utc;
        TableMemberInfoSql localMember = GetMember(nameof(TemporalModel.AventusLocalValue));
        var unspecified = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Unspecified);

        var utcStored = (DateTime)utcMember.TransformQueryValue(new Datetime(unspecified))!;
        var localStored = (DateTime)localMember.TransformQueryValue(new Datetime(utcStored))!;

        Assert.Multiple(() =>
        {
            Assert.That(utcStored, Is.EqualTo(
                DateTime.SpecifyKind(unspecified, DateTimeKind.Local).ToUniversalTime()));
            Assert.That(utcStored.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(localStored, Is.EqualTo(DateTime.SpecifyKind(
                utcStored.ToLocalTime(), DateTimeKind.Unspecified)));
            Assert.That(localStored.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        });
    }

    private static TableMemberInfoSql GetMember(string name)
    {
        var table = new TableInfo(typeof(TemporalModel));
        var initialized = table.Init();
        Assert.That(initialized.Success, Is.True);
        return table.Members.Single(member => member.Name == name);
    }
}
