using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharpTest.Integration.Models;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DataExternalIgnoreTests
{
    [Test]
    public async Task Ignore_nested_reverse_relation_field()
    {
        await TestProjectionWrapper.StartDelete().Where(item => item.Id > 0).RunWithError();
        await TestProjectionChild.StartDelete().Where(item => item.Id > 0).RunWithError();
        await TestProjectionParent.StartDelete().Where(item => item.Id > 0).RunWithError();

        var parent = (await TestProjectionParent.Create(
            new TestProjectionParent { Name = "Parent" }))!;
        var child = (await TestProjectionChild.Create(
            new TestProjectionChild { Name = "Excluded", Detail = "Included", Parent = parent }))!;
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
            .Where(item => item.Id == wrapper.Id).SingleWithError();

        Assert.That(result.Success, Is.True, IntegrationEnvironment.ErrorMessages(result.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.Not.SameAs(wrapper));
            Assert.That(result.Result!.Parent, Is.Not.SameAs(parent));
            Assert.That(result.Result.Parent.Child, Is.Not.SameAs(child));
            Assert.That(result.Result!.Parent.Name, Is.EqualTo("Parent"));
            Assert.That(result.Result.Parent.Child, Is.Not.Null);
            Assert.That(result.Result.Parent.Child!.Name, Is.Empty);
            Assert.That(result.Result.Parent.Child.Detail, Is.EqualTo("Included"));
        });
    }

    [Test]
    public async Task Ignore_reverse_relation_field_preserves_canonical_value()
    {
        var clearWrappers = await TestProjectionWrapper.StartDelete()
            .Where(item => item.Id > 0).RunWithError();
        var clearChildren = await TestProjectionChild.StartDelete()
            .Where(item => item.Id > 0).RunWithError();
        var clearParents = await TestProjectionParent.StartDelete()
            .Where(item => item.Id > 0).RunWithError();
        Assert.That(clearWrappers.Success && clearChildren.Success && clearParents.Success, Is.True,
            IntegrationEnvironment.ErrorMessages(clearWrappers.Errors.Concat(clearChildren.Errors).Concat(clearParents.Errors)));

        var parent = (await TestProjectionParent.Create(
            new TestProjectionParent { Name = "Parent" }))!;
        var child = (await TestProjectionChild.Create(
            new TestProjectionChild { Name = "Kept", Detail = "Loaded", Parent = parent }))!;

        var cached = await TestProjectionParent.StartQuery()
            .Where(item => item.Id == parent.Id).SingleWithError();
        Assert.That(cached.Success, Is.True, IntegrationEnvironment.ErrorMessages(cached.Errors));

        var ignored = await TestProjectionParent.StartQuery()
            .Ignore(item => item.Child!.Name)
            .Where(item => item.Id == parent.Id).SingleWithError();

        Assert.That(ignored.Success, Is.True, IntegrationEnvironment.ErrorMessages(ignored.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(ignored.Result!.Child, Is.Not.Null);
            Assert.That(ignored.Result.Child!.Id, Is.EqualTo(child.Id));
            Assert.That(ignored.Result.Child.Name, Is.EqualTo("Kept"));
            Assert.That(ignored.Result.Child.Detail, Is.EqualTo("Loaded"));
        });

        ((IDatabaseDM)GenericDM.Get<TestProjectionChild>())
            .RemoveRecordsItems<TestProjectionChild>([child.Id]);
        ((IDatabaseDM)GenericDM.Get<TestProjectionParent>())
            .RemoveRecordsItems<TestProjectionParent>([parent.Id]);

        var uncached = await TestProjectionParent.StartQuery()
            .Ignore(item => item.Child!.Name)
            .Where(item => item.Id == parent.Id).SingleWithError();
        Assert.That(uncached.Success, Is.True, IntegrationEnvironment.ErrorMessages(uncached.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(uncached.Result!.Child, Is.Not.Null);
            Assert.That(uncached.Result.Child!.Name, Is.Empty);
            Assert.That(uncached.Result.Child.Detail, Is.EqualTo("Loaded"));
        });
    }
}
