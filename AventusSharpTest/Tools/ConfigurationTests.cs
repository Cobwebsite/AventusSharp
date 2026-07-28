using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace AventusSharpTest.Tools;

[TestFixture]
[NonParallelizable]
public sealed class ConfigurationTests
{
    private const string PortEnvironmentName = "AVENTUS_TEST_CONFIGURATION_PORT";
    private const string ModeEnvironmentName = "AVENTUS_TEST_CONFIGURATION_MODE";
    private const string OptionalEnvironmentName = "AVENTUS_TEST_CONFIGURATION_OPTIONAL";
    private const string FieldEnvironmentName = "AVENTUS_TEST_CONFIGURATION_FIELD";

    [TearDown]
    public void ClearEnvironment()
    {
        Environment.SetEnvironmentVariable(PortEnvironmentName, null);
        Environment.SetEnvironmentVariable(ModeEnvironmentName, null);
        Environment.SetEnvironmentVariable(OptionalEnvironmentName, null);
        Environment.SetEnvironmentVariable(FieldEnvironmentName, null);
    }

    [Test]
    public void Read_binds_scalar_nested_and_collection_values()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Name"] = "server",
            ["Port"] = "8080",
            ["Nested:Enabled"] = "true",
            ["Values:0"] = "one",
            ["Values:1"] = "two",
        });

        var result = configuration.Read<ConfigurationModel>();

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("server"));
            Assert.That(result.Port, Is.EqualTo(8080));
            Assert.That(result.Nested.Enabled, Is.True);
            Assert.That(result.Values, Is.EqualTo(new[] { "one", "two" }));
        });
    }

    [Test]
    public void Environment_attributes_override_configuration_on_properties_and_fields()
    {
        Environment.SetEnvironmentVariable(PortEnvironmentName, "9090");
        Environment.SetEnvironmentVariable(ModeEnvironmentName, "production");
        Environment.SetEnvironmentVariable(OptionalEnvironmentName, "42");
        Environment.SetEnvironmentVariable(FieldEnvironmentName, "from-env");
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Port"] = "8080",
            ["Mode"] = "Development",
            ["Optional"] = "7",
            ["FieldValue"] = "from-config",
        });

        var result = configuration.Read<ConfigurationModel>();

        Assert.Multiple(() =>
        {
            Assert.That(result.Port, Is.EqualTo(9090));
            Assert.That(result.Mode, Is.EqualTo(ConfigurationMode.Production));
            Assert.That(result.Optional, Is.EqualTo(42));
            Assert.That(result.FieldValue, Is.EqualTo("from-env"));
        });
    }

    [Test]
    public void Missing_configuration_creates_an_instance_with_defaults()
    {
        var result = Build([]).Read<ConfigurationModel>();

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("default"));
            Assert.That(result.Port, Is.EqualTo(10));
            Assert.That(result.Nested, Is.Not.Null);
        });
    }

    [Test]
    public void Auto_configuration_loads_public_properties_fields_and_named_sections()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Renamed:Value"] = "section-value",
            ["Count"] = "12",
            ["PublicField"] = "field-value",
            ["Ignored"] = "should-not-be-used",
        });

        var result = new ApplicationConfiguration(configuration);

        Assert.Multiple(() =>
        {
            Assert.That(result.Section.Value, Is.EqualTo("section-value"));
            Assert.That(result.Count, Is.EqualTo(12));
            Assert.That(result.PublicField, Is.EqualTo("field-value"));
            Assert.That(result.Ignored, Is.EqualTo("initial"));
            Assert.That(result.ReadOnly, Is.EqualTo("read-only"));
        });
    }

    [Test]
    public void Invalid_environment_enum_value_is_rejected()
    {
        Environment.SetEnvironmentVariable(ModeEnvironmentName, "unknown-mode");

        Assert.Throws<ArgumentException>(() =>
            Build([]).Read<ConfigurationModel>());
    }

    private static IConfiguration Build(
        IEnumerable<KeyValuePair<string, string?>> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private enum ConfigurationMode
    {
        Development,
        Production,
    }

    private sealed class NestedConfiguration
    {
        public bool Enabled { get; set; }
    }

    private sealed class ConfigurationModel
    {
        public string Name { get; set; } = "default";

        [EnvName(PortEnvironmentName)]
        public int Port { get; set; } = 10;

        [EnvName(ModeEnvironmentName)]
        public ConfigurationMode Mode { get; set; }

        [EnvName(OptionalEnvironmentName)]
        public int? Optional { get; set; }

        [EnvName(FieldEnvironmentName)]
        public string FieldValue = "default-field";

        public NestedConfiguration Nested { get; set; } = new();
        public List<string> Values { get; set; } = [];
    }

    private sealed class NamedSection
    {
        public string Value { get; set; } = "";
    }

    private sealed class ApplicationConfiguration : AutoConfiguration
    {
        public ApplicationConfiguration(IConfiguration configuration)
            : base(configuration)
        {
        }

        [ConfigSection("Renamed")]
        public NamedSection Section { get; set; } = new();

        public int Count { get; set; }
        public string PublicField = "";

        [ConfigIgnore]
        public string Ignored { get; set; } = "initial";

        public string ReadOnly { get; } = "read-only";
    }
}
