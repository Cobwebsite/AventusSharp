using System.Text.Json;
using CSharpExport;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: csharp-export <config.json>");
    return 1;
}
var config = JsonSerializer.Deserialize<ProjectConfig>(File.ReadAllText(args[0]),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("Configuration vide.");
config.Prepare(args[0]);
return await new ProjectManager().Init(config);
