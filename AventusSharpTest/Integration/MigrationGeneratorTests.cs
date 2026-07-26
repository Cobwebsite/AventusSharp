using AventusSharp.Chart;
using AventusSharp.Data.Migrations;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
public sealed class MigrationGeneratorTests
{
    [Test]
    public void Generate_emits_properties_constraints_and_timestamp_shortcut()
    {
        var diagram = new DiagramObject("migration", "sqlite")
        {
            Tables =
            [
                Table("products",
                    Field("products.Id", "Id", "integer", primary: true),
                    Field("products.Name", "Name", "varchar(80)", unique: true),
                    Field("products.Description", "Description", "text", nullable: true),
                    Field("products.CreatedDate", "CreatedDate", "timestamp"),
                    Field("products.UpdatedDate", "UpdatedDate", "timestamp"))
            ]
        };

        var code = MigrationGenerator.Generate(diagram, "0001_products", "ProductsMigration");

        Assert.Multiple(() =>
        {
            Assert.That(code, Does.Contain("return \"0001_products\";"));
            Assert.That(code, Does.Contain("CreateModel<products>()"));
            Assert.That(code, Does.Contain("AddPrimary(\"Id\")"));
            Assert.That(code, Does.Contain("AddProperty<string>(\"Name\", new() { Unique = true, Size = new Size(80) })"));
            Assert.That(code, Does.Contain("AddProperty<string>(\"Description\", new() { Nullable = true, Size = new Size(SizeEnum.Text) })"));
            Assert.That(code, Does.Contain("AddTimestamp()"));
            Assert.That(code, Does.Contain("DeleteModel<products>();"));
        });
    }

    [Test]
    public void Generate_orders_referenced_tables_before_dependants_and_deletes_in_reverse()
    {
        var rooms = Table("Rooms", Field("Rooms.Id", "Id", "integer", primary: true));
        var lamps = Table("Lamps",
            Field("Lamps.Id", "Id", "integer", primary: true),
            Field("Lamps.Room", "Room", "integer"));
        var diagram = new DiagramObject("migration", "sqlite")
        {
            Tables = [lamps, rooms],
            Relationships =
            [
                new DiagramRelationship
                {
                    Name = "lamp_room",
                    SourceTableId = lamps.Id,
                    SourceFieldId = "Lamps.Room",
                    TargetTableId = rooms.Id,
                    TargetFieldId = "Rooms.Id"
                }
            ]
        };

        var code = MigrationGenerator.Generate(diagram, "0002_relations", "RelationsMigration");

        Assert.Multiple(() =>
        {
            Assert.That(code.IndexOf("CreateModel<Rooms>()", StringComparison.Ordinal),
                Is.LessThan(code.IndexOf("CreateModel<Lamps>()", StringComparison.Ordinal)));
            Assert.That(code, Does.Contain("AddRef<Rooms>(\"Room\")"));
            Assert.That(code.IndexOf("DeleteModel<Lamps>();", StringComparison.Ordinal),
                Is.LessThan(code.IndexOf("DeleteModel<Rooms>();", StringComparison.Ordinal)));
        });
    }

    private static DiagramTable Table(string name, params DiagramField[] fields) =>
        new()
        {
            Id = name,
            Name = name,
            Color = "#000000",
            Fields = fields.ToList()
        };

    private static DiagramField Field(
        string id,
        string name,
        string type,
        bool primary = false,
        bool unique = false,
        bool nullable = false) =>
        new()
        {
            Id = id,
            Name = name,
            Type = new DiagramFieldType { Id = type, Name = type },
            PrimaryKey = primary,
            Unique = unique,
            Nullable = nullable
        };
}
