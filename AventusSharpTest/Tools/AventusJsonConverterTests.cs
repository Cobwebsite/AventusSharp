using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace AventusSharpTest.Tools;

[TestFixture]
public sealed class AventusJsonConverterTests
{
    private static JsonSerializerSettings Settings()
    {
        return new JsonSerializerSettings
        {
            Converters = { new AventusJsonConverter() },
        };
    }

    [Test]
    public void Object_contains_runtime_type_public_properties_and_fields()
    {
        var value = new JsonModel
        {
            Name = "lamp",
            Count = 3,
            PublicField = "field",
        };

        var json = JsonConvert.SerializeObject(value, Settings());
        var result = JObject.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(result["$type"]?.Value<string>(),
                Does.StartWith(typeof(JsonModel).FullName!));
            Assert.That(result["Name"]?.Value<string>(), Is.EqualTo("lamp"));
            Assert.That(result["Count"]?.Value<int>(), Is.EqualTo(3));
            Assert.That(result["PublicField"]?.Value<string>(), Is.EqualTo("field"));
        });
    }

    [Test]
    public void No_export_members_and_null_values_are_omitted()
    {
        var value = new JsonModel
        {
            Name = null,
            Secret = "hidden",
            HiddenField = "hidden-field",
        };

        var result = JObject.Parse(
            JsonConvert.SerializeObject(value, Settings()));

        Assert.Multiple(() =>
        {
            Assert.That(result.ContainsKey(nameof(JsonModel.Name)), Is.False);
            Assert.That(result.ContainsKey(nameof(JsonModel.Secret)), Is.False);
            Assert.That(result.ContainsKey(nameof(JsonModel.HiddenField)), Is.False);
        });
    }

    [Test]
    public void Generic_dictionary_is_exported_as_an_aventus_map()
    {
        var value = new Dictionary<string, int>
        {
            ["first"] = 1,
            ["second"] = 2,
        };

        var result = JObject.Parse(
            JsonConvert.SerializeObject(value, Settings()));
        var entries = (JArray)result["values"]!;

        Assert.Multiple(() =>
        {
            Assert.That(result["$type"]?.Value<string>(), Is.EqualTo("Aventus.Map"));
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries.Select(entry => entry![0]!.Value<string>()),
                Is.EquivalentTo(new[] { "first", "second" }));
            Assert.That(entries.Select(entry => entry![1]!.Value<int>()),
                Is.EquivalentTo(new[] { 1, 2 }));
        });
    }

    [Test]
    public void Generic_list_is_exported_as_a_json_array()
    {
        var json = JsonConvert.SerializeObject(
            new List<int> { 2, 4, 6 },
            Settings());

        Assert.That(JArray.Parse(json).Values<int>(), Is.EqualTo(new[] { 2, 4, 6 }));
    }

    [Test]
    public void Nested_objects_are_processed_by_the_converter()
    {
        var value = new JsonContainer
        {
            Child = new JsonModel { Name = "nested" },
        };

        var result = JObject.Parse(
            JsonConvert.SerializeObject(value, Settings()));
        var child = (JObject)result["Child"]!;

        Assert.Multiple(() =>
        {
            Assert.That(child["$type"], Is.Not.Null);
            Assert.That(child["Name"]?.Value<string>(), Is.EqualTo("nested"));
        });
    }

    [Test]
    public void Converter_can_read_a_regular_json_object()
    {
        const string json = """{"Name":"restored","Count":8,"PublicField":"value"}""";

        var result = JsonConvert.DeserializeObject<JsonModel>(json, Settings());

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo("restored"));
            Assert.That(result.Count, Is.EqualTo(8));
            Assert.That(result.PublicField, Is.EqualTo("value"));
        });
    }

    [Test]
    public void Primitive_enum_and_null_values_keep_standard_json_shapes()
    {
        var settings = Settings();

        Assert.Multiple(() =>
        {
            Assert.That(JsonConvert.SerializeObject(12, settings), Is.EqualTo("12"));
            Assert.That(JsonConvert.SerializeObject(TestState.Active, settings), Is.EqualTo("1"));
            Assert.That(JsonConvert.SerializeObject(null, settings), Is.EqualTo("null"));
        });
    }

    [Test]
    public void Reader_settings_are_isolated_between_serializer_instances()
    {
        var strict = Settings();
        strict.MissingMemberHandling = MissingMemberHandling.Error;
        var permissive = Settings();
        permissive.MissingMemberHandling = MissingMemberHandling.Ignore;

        var first = JsonConvert.DeserializeObject<JsonModel>(
            """{"Name":"strict"}""",
            strict);
        JsonModel? second = null;
        Assert.DoesNotThrow(() =>
            second = JsonConvert.DeserializeObject<JsonModel>(
                """{"Name":"permissive","Unknown":true}""",
                permissive));

        Assert.Multiple(() =>
        {
            Assert.That(first?.Name, Is.EqualTo("strict"));
            Assert.That(second?.Name, Is.EqualTo("permissive"));
        });
    }

    [Test]
    public void Concurrent_readers_do_not_share_serializer_settings()
    {
        var failures = new ConcurrentQueue<Exception>();

        Parallel.For(0, 100, index =>
        {
            try
            {
                var settings = Settings();
                settings.MissingMemberHandling =
                    index % 2 == 0
                        ? MissingMemberHandling.Error
                        : MissingMemberHandling.Ignore;
                var json = index % 2 == 0
                    ? """{"Name":"known"}"""
                    : """{"Name":"known","Unknown":true}""";
                var value = JsonConvert.DeserializeObject<JsonModel>(
                    json,
                    settings);
                if (value?.Name != "known")
                {
                    throw new AssertionException("The model was not deserialized.");
                }
            }
            catch (Exception exception)
            {
                failures.Enqueue(exception);
            }
        });

        Assert.That(failures, Is.Empty);
    }

    private enum TestState
    {
        Inactive,
        Active,
    }

    private sealed class JsonContainer
    {
        public JsonModel? Child { get; set; }
    }

    private sealed class JsonModel
    {
        public string? Name { get; set; }
        public int Count { get; set; }

        [NoExport]
        public string? Secret { get; set; }

        public string? PublicField;

        [NoExport]
        public string? HiddenField;
    }
}
