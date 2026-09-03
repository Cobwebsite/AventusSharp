using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Tools;

[TestFixture]
public sealed class TypeToolsTests
{
    [TestCase(typeof(int), true)]
    [TestCase(typeof(int?), true)]
    [TestCase(typeof(string), true)]
    [TestCase(typeof(DateTime), true)]
    [TestCase(typeof(TimeSpan?), true)]
    [TestCase(typeof(TimeOnly), true)]
    [TestCase(typeof(TimeOnly?), true)]
    [TestCase(typeof(Guid), false)]
    [TestCase(typeof(TypeToolsTests), false)]
    public void Primitive_type_detection_handles_nullable_and_reference_types(
        Type type,
        bool expected)
    {
        Assert.That(TypeTools.IsPrimitiveType(type), Is.EqualTo(expected));
    }

    [Test]
    public void Primitive_type_detection_rejects_null()
    {
        Assert.That(TypeTools.IsPrimitiveType(null), Is.False);
    }

    [Test]
    public void Readable_name_formats_nested_generic_arguments()
    {
        var name = TypeTools.GetReadableName(
            typeof(Dictionary<string, List<int?>>));

        Assert.That(name, Is.EqualTo("Dictionary<String,List<Nullable<Int32>>>"));
    }

    [Test]
    public void Create_new_object_uses_the_parameterless_constructor()
    {
        var result = TypeTools.CreateNewObj<ConstructedModel>();

        Assert.That(result.Value, Is.EqualTo("constructed"));
    }

    [Test]
    public void Member_name_returns_a_nested_property_path()
    {
        var result = TypeTools.GetMemberName<ParentModel, string?>(
            model => model.Child!.Name);

        Assert.That(result, Is.EqualTo("Child.Name"));
    }

    [Test]
    public void Member_name_returns_empty_for_a_non_member_expression()
    {
        var result = TypeTools.GetMemberName<ParentModel, int>(
            model => model.Child == null ? 0 : 1);

        Assert.That(result, Is.Empty);
    }

    private sealed class ConstructedModel
    {
        public string Value { get; } = "constructed";
    }

    private sealed class ParentModel
    {
        public ChildModel? Child { get; set; }
    }

    private sealed class ChildModel
    {
        public string? Name { get; set; }
    }
}
