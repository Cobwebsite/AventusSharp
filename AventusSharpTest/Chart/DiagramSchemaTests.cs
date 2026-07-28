using AventusSharp.Chart;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace AventusSharpTest.Chart;

[TestFixture]
public sealed class DiagramSchemaTests
{
    [TestCase(0, 42)]
    [TestCase(1, 74)]
    [TestCase(10, 362)]
    [TestCase(11, 394)]
    [TestCase(50, 394)]
    public void Table_height_accounts_for_visible_fields_and_show_more_footer(
        int fieldCount,
        double expected)
    {
        var diagram = new DiagramObject("test", "sqlite");

        Assert.That(diagram.CalculateTableHeight(fieldCount), Is.EqualTo(expected));
    }

    [Test]
    public void Layout_places_area_tables_in_a_grid_and_sizes_the_area()
    {
        var diagram = new DiagramObject("test", "sqlite");
        var area = new Area { Id = "area", Name = "Devices" };
        diagram.Areas.Add(area);
        diagram.Tables.AddRange(
        [
            Table("one", "One", "area", 1),
            Table("two", "Two", "area", 2),
            Table("three", "Three", "area", 11),
            Table("four", "Four", "area", 3),
        ]);

        diagram.LayoutDiagram();

        Assert.Multiple(() =>
        {
            Assert.That(area.X, Is.EqualTo(100));
            Assert.That(area.Y, Is.EqualTo(100));
            Assert.That(area.Width, Is.EqualTo(588));
            Assert.That(area.Height, Is.EqualTo(640));
            Assert.That(diagram.Tables[0].X, Is.EqualTo(140));
            Assert.That(diagram.Tables[0].Y, Is.EqualTo(140));
            Assert.That(diagram.Tables[1].X, Is.EqualTo(424));
            Assert.That(diagram.Tables[1].Y, Is.EqualTo(140));
            Assert.That(diagram.Tables[2].X, Is.EqualTo(140));
            Assert.That(diagram.Tables[2].Y, Is.EqualTo(306));
        });
    }

    [Test]
    public void Layout_positions_tables_without_an_area_after_declared_areas()
    {
        var diagram = new DiagramObject("test", "sqlite");
        diagram.Areas.Add(new Area { Id = "area", Name = "Area" });
        diagram.Tables.Add(Table("inside", "Inside", "area", 1));
        diagram.Tables.Add(Table("outside", "Outside", null, 1));

        diagram.LayoutDiagram();

        Assert.That(diagram.Tables[1].X, Is.GreaterThan(diagram.Areas[0].X));
        Assert.That(diagram.Tables[1].Y, Is.EqualTo(140));
    }

    [Test]
    public void Merge_preserves_visual_identity_and_existing_field_ids()
    {
        var existing = new DiagramObject("test", "sqlite");
        existing.Tables.Add(new DiagramTable
        {
            Id = "persisted-table",
            Name = "devices",
            Color = "#123456",
            X = 450,
            Y = 320,
            Fields =
            [
                Field("persisted-id", "Id"),
                Field("persisted-name", "Name"),
            ],
        });
        var generated = new DiagramObject("test", "sqlite");
        generated.Tables.Add(new DiagramTable
        {
            Id = "generated-table",
            Name = "devices",
            Color = "#ffffff",
            Fields =
            [
                Field("generated-id", "Id"),
                Field("generated-name", "Name"),
                Field("generated-state", "State"),
            ],
        });

        existing.Merge(generated);
        var merged = existing.Tables.Single();

        Assert.Multiple(() =>
        {
            Assert.That(merged.Id, Is.EqualTo("persisted-table"));
            Assert.That(merged.Color, Is.EqualTo("#123456"));
            Assert.That(merged.X, Is.EqualTo(450));
            Assert.That(merged.Y, Is.EqualTo(320));
            Assert.That(merged.Fields.Single(field => field.Name == "Id").Id,
                Is.EqualTo("persisted-id"));
            Assert.That(merged.Fields.Single(field => field.Name == "Name").Id,
                Is.EqualTo("persisted-name"));
            Assert.That(merged.Fields.Single(field => field.Name == "State").Id,
                Is.EqualTo("persisted-table.State"));
        });
    }

    [Test]
    public void Merge_reuses_area_by_name_and_maps_a_new_table_to_its_id()
    {
        var existing = new DiagramObject("test", "sqlite");
        existing.Areas.Add(new Area
        {
            Id = "persisted-area",
            Name = "Devices",
            X = 200,
            Y = 300,
        });
        var generated = new DiagramObject("test", "sqlite");
        generated.Areas.Add(new Area { Id = "generated-area", Name = "Devices" });
        generated.Tables.Add(Table("new-table", "sensors", "generated-area", 1));

        existing.Merge(generated);

        Assert.Multiple(() =>
        {
            Assert.That(existing.Areas.Single().Id, Is.EqualTo("persisted-area"));
            Assert.That(existing.Tables.Single().ParentAreaId, Is.EqualTo("persisted-area"));
            Assert.That(existing.Tables.Single().X, Is.EqualTo(240));
            Assert.That(existing.Tables.Single().Y, Is.EqualTo(340));
        });
    }

    [Test]
    public void Merge_remaps_relationships_to_preserved_table_and_field_ids()
    {
        var existing = new DiagramObject("test", "sqlite");
        existing.Tables.Add(new DiagramTable
        {
            Id = "old-device",
            Name = "devices",
            Color = "#111111",
            Fields = [Field("old-device.Id", "Id")],
        });
        existing.Tables.Add(new DiagramTable
        {
            Id = "old-reading",
            Name = "readings",
            Color = "#222222",
            Fields = [Field("old-reading.DeviceId", "DeviceId")],
        });
        existing.Relationships.Add(new DiagramRelationship
        {
            Id = "persisted-relationship",
            Name = "readings_devices",
            SourceTableId = "old-reading",
            SourceFieldId = "old-reading.DeviceId",
            TargetTableId = "old-device",
            TargetFieldId = "old-device.Id",
        });
        var generated = new DiagramObject("test", "sqlite");
        generated.Tables.Add(new DiagramTable
        {
            Id = "new-device",
            Name = "devices",
            Color = "#ffffff",
            Fields = [Field("new-device.Id", "Id")],
        });
        generated.Tables.Add(new DiagramTable
        {
            Id = "new-reading",
            Name = "readings",
            Color = "#ffffff",
            Fields = [Field("new-reading.DeviceId", "DeviceId")],
        });
        generated.Relationships.Add(new DiagramRelationship
        {
            Name = "generated",
            SourceTableId = "new-reading",
            SourceFieldId = "new-reading.DeviceId",
            TargetTableId = "new-device",
            TargetFieldId = "new-device.Id",
        });

        existing.Merge(generated);
        var relationship = existing.Relationships.Single();
        var reading = existing.Tables.Single(table => table.Name == "readings");

        Assert.Multiple(() =>
        {
            Assert.That(relationship.Id, Is.EqualTo("persisted-relationship"));
            Assert.That(relationship.Name, Is.EqualTo("readings_devices"));
            Assert.That(relationship.SourceTableId, Is.EqualTo(reading.Id));
            Assert.That(relationship.SourceFieldId, Is.EqualTo($"{reading.Id}.DeviceId"));
            Assert.That(relationship.TargetTableId, Is.EqualTo("old-device"));
            Assert.That(relationship.TargetFieldId, Is.EqualTo("old-device.Id"));
        });
    }

    [Test]
    public void Repeated_merge_of_the_same_schema_is_idempotent()
    {
        var persisted = new DiagramObject("test", "sqlite");

        persisted.Merge(GeneratedRelatedSchema());
        var firstJson = JToken.Parse(JsonConvert.SerializeObject(persisted));
        var firstRelationshipId = persisted.Relationships.Single().Id;

        persisted.Merge(GeneratedRelatedSchema());
        var secondJson = JToken.Parse(JsonConvert.SerializeObject(persisted));

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Relationships.Single().Id,
                Is.EqualTo(firstRelationshipId));
            Assert.That(JToken.DeepEquals(secondJson, firstJson), Is.True);
        });
    }

    [Test]
    public void Merge_removes_tables_fields_and_relations_missing_from_the_new_schema()
    {
        var persisted = new DiagramObject("test", "sqlite");
        persisted.Tables.Add(new DiagramTable
        {
            Id = "devices",
            Name = "devices",
            Color = "#ffffff",
            Fields =
            [
                Field("devices.Id", "Id"),
                Field("devices.Obsolete", "Obsolete"),
            ],
        });
        persisted.Tables.Add(new DiagramTable
        {
            Id = "obsolete-table",
            Name = "obsolete",
            Color = "#ffffff",
            Fields = [Field("obsolete.Id", "Id")],
        });
        persisted.Relationships.Add(new DiagramRelationship
        {
            Id = "obsolete-relation",
            Name = "obsolete_devices",
            SourceTableId = "obsolete-table",
            SourceFieldId = "obsolete.Id",
            TargetTableId = "devices",
            TargetFieldId = "devices.Id",
        });
        var generated = new DiagramObject("test", "sqlite");
        generated.Tables.Add(new DiagramTable
        {
            Id = "new-devices",
            Name = "devices",
            Color = "#ffffff",
            Fields = [Field("new-devices.Id", "Id")],
        });

        persisted.Merge(generated);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Tables.Select(table => table.Name),
                Is.EqualTo(new[] { "devices" }));
            Assert.That(persisted.Tables.Single().Fields.Select(field => field.Name),
                Is.EqualTo(new[] { "Id" }));
            Assert.That(persisted.Relationships, Is.Empty);
        });
    }

    [Test]
    public void Merge_preserves_distinct_ids_for_multiple_relationships_between_tables()
    {
        var persisted = RelatedSchemaWithTwoFields(
            "persisted-device",
            "persisted-reading");
        persisted.Relationships[0].Id = "relation-primary";
        persisted.Relationships[1].Id = "relation-secondary";
        var generated = RelatedSchemaWithTwoFields(
            "generated-device",
            "generated-reading");

        persisted.Merge(generated);

        Assert.That(
            persisted.Relationships.Select(relation => relation.Id),
            Is.EquivalentTo(new[] { "relation-primary", "relation-secondary" }));
    }

    [Test]
    public void A_changed_relation_gets_a_new_id_while_unchanged_relation_keeps_its_id()
    {
        var persisted = RelatedSchemaWithTwoFields(
            "persisted-device",
            "persisted-reading");
        persisted.Relationships[0].Id = "stable-relation";
        var oldChangedId = persisted.Relationships[1].Id;
        var generated = RelatedSchemaWithTwoFields(
            "generated-device",
            "generated-reading");
        generated.Relationships[1].TargetFieldId =
            "generated-device.SecondaryId";

        persisted.Merge(generated);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Relationships[0].Id,
                Is.EqualTo("stable-relation"));
            Assert.That(persisted.Relationships[1].Id,
                Is.Not.EqualTo(oldChangedId));
        });
    }

    [Test]
    public void Area_removed_from_generated_schema_is_removed_from_the_merge()
    {
        var persisted = new DiagramObject("test", "sqlite");
        persisted.Areas.Add(new Area { Id = "kept", Name = "Kept" });
        persisted.Areas.Add(new Area { Id = "removed", Name = "Removed" });
        var generated = new DiagramObject("test", "sqlite");
        generated.Areas.Add(new Area { Id = "new-kept", Name = "Kept" });

        persisted.Merge(generated);

        Assert.That(persisted.Areas.Select(area => (area.Id, area.Name)),
            Is.EqualTo(new[] { ("kept", "Kept") }));
    }

    [Test]
    public void Json_schema_uses_chartdb_property_names()
    {
        var diagram = new DiagramObject("home", "sqlite");
        diagram.Tables.Add(Table("devices", "devices", null, 1));

        var json = JObject.Parse(JsonConvert.SerializeObject(diagram));

        Assert.Multiple(() =>
        {
            Assert.That(json["name"]?.Value<string>(), Is.EqualTo("home"));
            Assert.That(json["databaseType"]?.Value<string>(), Is.EqualTo("sqlite"));
            Assert.That(json["tables"], Is.TypeOf<JArray>());
            Assert.That(json["relationships"], Is.TypeOf<JArray>());
            Assert.That(json["areas"], Is.TypeOf<JArray>());
            Assert.That(json["tables"]![0]!["fields"]![0]!["type"]!["name"]?.Value<string>(),
                Is.EqualTo("int"));
        });
    }

    private static DiagramTable Table(
        string id,
        string name,
        string? areaId,
        int fields)
    {
        return new DiagramTable
        {
            Id = id,
            Name = name,
            Color = "#ffffff",
            ParentAreaId = areaId,
            Fields = Enumerable.Range(0, fields)
                .Select(index => Field($"{id}.Field{index}", $"Field{index}"))
                .ToList(),
        };
    }

    private static DiagramField Field(string id, string name)
    {
        return new DiagramField
        {
            Id = id,
            Name = name,
            Type = new DiagramFieldType { Id = "integer", Name = "int" },
        };
    }

    private static DiagramObject GeneratedRelatedSchema()
    {
        var diagram = new DiagramObject("test", "sqlite");
        var deviceId = Guid.NewGuid().ToString();
        var readingId = Guid.NewGuid().ToString();
        diagram.Tables.Add(new DiagramTable
        {
            Id = deviceId,
            Name = "devices",
            Color = "#ffffff",
            Fields = [Field($"{deviceId}.Id", "Id")],
        });
        diagram.Tables.Add(new DiagramTable
        {
            Id = readingId,
            Name = "readings",
            Color = "#ffffff",
            Fields = [Field($"{readingId}.DeviceId", "DeviceId")],
        });
        diagram.Relationships.Add(new DiagramRelationship
        {
            Name = "generated",
            SourceTableId = readingId,
            SourceFieldId = $"{readingId}.DeviceId",
            TargetTableId = deviceId,
            TargetFieldId = $"{deviceId}.Id",
        });
        return diagram;
    }

    private static DiagramObject RelatedSchemaWithTwoFields(
        string deviceId,
        string readingId)
    {
        var diagram = new DiagramObject("test", "sqlite");
        diagram.Tables.Add(new DiagramTable
        {
            Id = deviceId,
            Name = "devices",
            Color = "#ffffff",
            Fields =
            [
                Field($"{deviceId}.Id", "Id"),
                Field($"{deviceId}.SecondaryId", "SecondaryId"),
            ],
        });
        diagram.Tables.Add(new DiagramTable
        {
            Id = readingId,
            Name = "readings",
            Color = "#ffffff",
            Fields =
            [
                Field($"{readingId}.DeviceId", "DeviceId"),
                Field($"{readingId}.SecondaryDeviceId", "SecondaryDeviceId"),
            ],
        });
        diagram.Relationships.Add(new DiagramRelationship
        {
            Name = "primary",
            SourceTableId = readingId,
            SourceFieldId = $"{readingId}.DeviceId",
            TargetTableId = deviceId,
            TargetFieldId = $"{deviceId}.Id",
        });
        diagram.Relationships.Add(new DiagramRelationship
        {
            Name = "secondary",
            SourceTableId = readingId,
            SourceFieldId = $"{readingId}.SecondaryDeviceId",
            TargetTableId = deviceId,
            TargetFieldId = $"{deviceId}.Id",
        });
        return diagram;
    }
}
