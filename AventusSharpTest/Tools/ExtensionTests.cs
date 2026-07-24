using System.Collections;
using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Tools;

[TestFixture]
public class ExtensionTests
{
    [Test]
    public void Numeric_extensions_match_system_math()
    {
        Assert.Multiple(() =>
        {
            Assert.That((-12).Abs(), Is.EqualTo(12));
            Assert.That(3.6d.Round(), Is.EqualTo(4d));
            Assert.That(3.1d.Ceil(), Is.EqualTo(4d));
            Assert.That(3.9d.Floor(), Is.EqualTo(3d));
        });
    }

    [Test]
    public void List_conversion_keeps_only_assignable_values()
    {
        IList values = new ArrayList { "one", 2, "three", null };

        Assert.That(values.ToList<string>(), Is.EqualTo(new[] { "one", "three" }));
        Assert.That(values.ToListOfType(typeof(int)), Is.EqualTo(new[] { 2 }));
    }

    [TestCase(typeof(int?), true)]
    [TestCase(typeof(int), false)]
    [TestCase(typeof(string), false)]
    public void Nullable_detection_handles_value_types(Type type, bool expected)
    {
        Assert.That(type.IsNullable(), Is.EqualTo(expected));
    }
}
