using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataQueryProjectionTests
{
    private Device device = null!;

    [SetUp]
    public async Task SetUp()
    {
        var clear = await Device.StartDelete()
            .Where(item => item.Id > 0)
            .RunWithError();
        Assert.That(clear.Success, Is.True, IntegrationEnvironment.ErrorMessages(clear.Errors));

        device = (await Device.Create(NewDevice()))!;
        Assert.That(device, Is.Not.Null);
    }

    [Test]
    public async Task Field_only_materializes_the_selected_persistent_member()
    {
        var result = await Device.StartQuery()
            .Field(item => item.Name)
            .Where(item => item.Id == device.Id)
            .SingleWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result!.Name, Is.EqualTo("Projection"));
            Assert.That(result.Result.Room, Is.Empty);
            Assert.That(result.Result.Brightness, Is.Zero);
            Assert.That(result.Result.RuntimeState, Is.Empty);
        });
    }

    [Test]
    public async Task Ignore_excludes_only_the_requested_member()
    {
        var result = await Device.StartQuery()
            .Ignore(item => item.Brightness)
            .Where(item => item.Id == device.Id)
            .SingleWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result!.Id, Is.EqualTo(device.Id));
            Assert.That(result.Result.Name, Is.EqualTo("Projection"));
            Assert.That(result.Result.Room, Is.EqualTo("Office"));
            Assert.That(result.Result.Brightness, Is.Zero);
            Assert.That(result.Result.IsOnline, Is.True);
        });
    }

    [Test]
    public async Task Fields_restores_all_members_after_selecting_one_field()
    {
        var result = await Device.StartQuery()
            .Field(item => item.Name)
            .Fields()
            .Where(item => item.Id == device.Id)
            .SingleWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result!.Id, Is.EqualTo(device.Id));
            Assert.That(result.Result.Name, Is.EqualTo("Projection"));
            Assert.That(result.Result.Room, Is.EqualTo("Office"));
            Assert.That(result.Result.Brightness, Is.EqualTo(65));
            Assert.That(result.Result.PowerConsumption, Is.EqualTo(6.5));
            Assert.That(result.Result.IsOnline, Is.True);
        });
    }

    [Test]
    public async Task Projection_without_Id_does_not_add_an_unidentifiable_item_to_the_cache()
    {
        var manager = AventusSharp.Data.Manager.GenericDM.Get<Device>();
        ((AventusSharp.Data.Manager.DB.IDatabaseDM)manager)
            .RemoveRecordsItems<Device>([device.Id]);

        var projection = await Device.StartQuery()
            .Field(item => item.Name)
            .Where(item => item.Id == device.Id)
            .RunWithError();
        var byId = await manager.GetByIdWithError<Device>(device.Id);

        Assert.That(projection.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(projection.Errors));
        Assert.That(projection.Result, Has.Count.EqualTo(1));
        Assert.That(projection.Result![0].Id, Is.Zero);
        Assert.That(byId.Success, Is.True, IntegrationEnvironment.ErrorMessages(byId.Errors));
        Assert.That(byId.Result!.Id, Is.EqualTo(device.Id));
        Assert.That(byId.Result.Name, Is.EqualTo("Projection"));
        Assert.That(byId.Result, Is.Not.SameAs(projection.Result[0]));
    }

    private static Device NewDevice() =>
        new()
        {
            Name = "Projection",
            Room = "Office",
            Brightness = 65,
            PowerConsumption = 6.5,
            IsOnline = true,
            InstalledOn = new AventusSharp.Data.Date(new DateTime(2026, 2, 3)),
            LastSeen = new AventusSharp.Data.Datetime(new DateTime(2026, 2, 3, 4, 5, 6)),
            RuntimeState = "memory only"
        };
}
