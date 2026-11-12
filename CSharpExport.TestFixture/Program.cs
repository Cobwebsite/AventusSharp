using System;
using System.IO;
using System.Linq;
if (args.Contains("--export-info"))
{
    Console.WriteLine("--- Routes HTTP ---");
    Console.WriteLine(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "compiled-routes-full.json")));
    Console.WriteLine("-------------------");
}
