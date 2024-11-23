// See https://aka.ms/new-console-template for more information
using CSharpToTypescript;
using Newtonsoft.Json;

if (args.Length >= 1)
{
    string configPath = args[0];
    ProjectConfig? config = JsonConvert.DeserializeObject<ProjectConfig>(File.ReadAllText(configPath));
    if(config != null)
    {
        config.Prepare(configPath);
        Console.WriteLine(config.csProj);
        Console.WriteLine(config.outputPath);
        await new ProjectManager().Init(config);
    }
    else
    {
        Console.WriteLine("Error : Can't load the config file");
    }
}
else
{
    Console.WriteLine("Error : Can't load the config file");
    return;
    ProjectConfig config = new ProjectConfig();
    if (false)
    {
        config.csProj = "D:\\Aventus\\AvenutsSharp\\AvenutsSharp\\AventusSharp.csproj";
        config.outputPath = "D:\\Aventus\\AvenutsSharp\\AvenutsSharp\\AventusJs\\src\\generated";
    }
    if (false)
    {
        config.csProj = @"D:\Rayuki\Core\BaseApp\BaseApp.csproj";
        config.outputPath = @"D:\Rayuki\Core\BaseApp\Front\src";
    }
    if (true)
    {
        config.csProj = "D:\\Rayuki\\Core\\Core\\Core.csproj";
        config.outputPath = "D:\\Rayuki\\Core\\Core\\Front\\generated";
    }
    await new ProjectManager().Init(config);
}

Console.WriteLine("Done");