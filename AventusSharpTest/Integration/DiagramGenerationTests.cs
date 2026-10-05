using AventusSharp.Chart;
using NUnit.Framework;

namespace AventusSharpTest.Integration;

[TestFixture]
[NonParallelizable]
public sealed class DiagramGenerationTests
{
    [Test]
    public void Diagram_is_generated_from_registered_models()
    {
        var diagrams = IntegrationEnvironment.Storage.GetDiagrams(new DiagramConfig
        {
            GenerateMain = true,
            MainName = "AventusTests",
            OutputDirectory = ""
        }.ToInternal());

        var diagram = diagrams.Single(value => value.Name == "AventusTests");
        var device = diagram.Tables.Single(table => table.Name == "devices");

        Assert.That(device.Fields.Select(field => field.Name), Does.Contain("Name"));
        Assert.That(device.Fields.Select(field => field.Name), Does.Contain("Brightness"));
        Assert.That(device.Fields.Select(field => field.Name), Does.Not.Contain("RuntimeState"));
        Assert.That(device.Fields.Single(field => field.Name == "Id").PrimaryKey, Is.True);
    }

    [Test]
    public void Multiple_links_to_the_same_table_have_distinct_names_and_source_fields()
    {
        var diagram = IntegrationEnvironment.Storage.GetDiagrams(new DiagramConfig
        {
            GenerateMain = true,
            MainName = "AventusTests",
            OutputDirectory = ""
        }.ToInternal()).Single(value => value.Name == "AventusTests");

        var source = diagram.Tables.Single(table => table.Name == "diagram_sources");
        var target = diagram.Tables.Single(table => table.Name == "diagram_targets");
        var relationships = diagram.Relationships
            .Where(relation => relation.SourceTableId == source.Id && relation.TargetTableId == target.Id)
            .ToDictionary(relation => relation.SourceFieldId);

        Assert.Multiple(() =>
        {
            Assert.That(relationships.Keys, Is.EquivalentTo(new[]
            {
                "diagram_sources.PrimaryTarget",
                "diagram_sources.backup_target_id"
            }));
            Assert.That(relationships.Values.Select(relation => relation.Name), Is.EquivalentTo(new[]
            {
                "diagram_sources_PrimaryTarget_diagram_targets",
                "diagram_sources_backup_target_id_diagram_targets"
            }));
            Assert.That(relationships.Values.All(relation => relation.TargetFieldId == "diagram_targets.Id"), Is.True);
            Assert.That(relationships.Values.All(relation =>
                diagram.Tables.Any(table => table.Id == relation.SourceTableId)
                && diagram.Tables.Any(table => table.Id == relation.TargetTableId)
                && source.Fields.Any(field => field.Id == relation.SourceFieldId)
                && target.Fields.Any(field => field.Id == relation.TargetFieldId)), Is.True);
        });
    }
}
