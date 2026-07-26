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
}
