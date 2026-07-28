using AventusSharp.Tools;
using NUnit.Framework;

namespace AventusSharpTest.Tools;

[TestFixture]
public sealed class AutoBindTests
{
    [Test]
    public void Generic_bind_copies_matching_properties_and_converts_simple_values()
    {
        var source = new SourceModel
        {
            Name = "lamp",
            CountText = "12",
        };

        var target = Binder.AutoBind<TargetModel>(source);

        Assert.Multiple(() =>
        {
            Assert.That(target.Name, Is.EqualTo("lamp"));
            Assert.That(target.Count, Is.EqualTo(12));
        });
    }

    [Test]
    public void Bind_attribute_selects_a_differently_named_source_property()
    {
        var target = Binder.AutoBind<TargetModel>(
            new SourceModel { AlternateName = "renamed" });

        Assert.That(target.Label, Is.EqualTo("renamed"));
    }

    [Test]
    public void Convert_attribute_applies_a_custom_transformation()
    {
        var target = Binder.AutoBind<TargetModel>(
            new SourceModel { Name = "mixedCase" });

        Assert.That(target.UpperName, Is.EqualTo("MIXEDCASE"));
    }

    [Test]
    public void Null_source_values_do_not_erase_target_defaults()
    {
        var target = new TargetModel { Name = "existing" };

        Binder.AutoBind(new SourceModel { Name = null }, target);

        Assert.That(target.Name, Is.EqualTo("existing"));
    }

    [Test]
    public void Missing_read_only_and_incompatible_properties_are_ignored()
    {
        var target = new TargetModel();
        var source = new SourceModel
        {
            Incompatible = "not-a-guid",
            ReadOnlySourceValue = "ignored",
        };

        Assert.DoesNotThrow(() => Binder.AutoBind(source, target));
        Assert.Multiple(() =>
        {
            Assert.That(target.Incompatible, Is.EqualTo(Guid.Empty));
            Assert.That(target.ReadOnlyTarget, Is.EqualTo("read-only"));
            Assert.That(target.Missing, Is.EqualTo("missing-default"));
        });
    }

    [Test]
    public void Null_source_leaves_an_existing_target_unchanged()
    {
        var target = new TargetModel { Name = "existing" };

        Assert.DoesNotThrow(() => Binder.AutoBind(null!, target));
        Assert.That(target.Name, Is.EqualTo("existing"));
    }

    [Test]
    public void Runtime_type_bind_returns_the_created_and_populated_instance()
    {
        var source = new SourceModel
        {
            Name = "dynamic",
            CountText = "27",
        };

        var result = Binder.AutoBind(source, typeof(TargetModel));

        Assert.That(result, Is.TypeOf<TargetModel>());
        var target = (TargetModel)result;
        Assert.Multiple(() =>
        {
            Assert.That(target.Name, Is.EqualTo("dynamic"));
            Assert.That(target.Count, Is.EqualTo(27));
        });
    }

    private sealed class SourceModel
    {
        public string? Name { get; set; }
        public string? AlternateName { get; set; }
        public string? CountText { get; set; }
        public string? Incompatible { get; set; }
        public string ReadOnlySourceValue { get; set; } = "";
    }

    private sealed class TargetModel
    {
        public string? Name { get; set; }

        [Bind(nameof(SourceModel.AlternateName))]
        public string? Label { get; set; }

        [Bind(nameof(SourceModel.CountText))]
        public int Count { get; set; }

        [Bind(nameof(SourceModel.Name))]
        [UpperCase]
        public string? UpperName { get; set; }

        public Guid Incompatible { get; set; }
        public string ReadOnlyTarget { get; } = "read-only";
        public string Missing { get; set; } = "missing-default";
    }

    private sealed class UpperCaseAttribute : AventusSharp.Tools.Convert<string, string>
    {
        public override string Transform(string from)
        {
            return from.ToUpperInvariant();
        }
    }
}
