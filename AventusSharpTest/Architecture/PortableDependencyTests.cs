using NUnit.Framework;

namespace AventusSharpTest.Architecture;

public sealed class PortableDependencyTests
{
    [TestCase("AventusSharp.Core")]
    [TestCase("AventusSharp.Maui")]
    [TestCase("AventusSharp.Data")]
    [TestCase("AventusSharp.Data.Sqlite")]
    [TestCase("AventusSharp.Runtime")]
    public void Portable_projects_do_not_reference_AspNetCore(string projectName)
    {
        string projectDirectory = FindProjectDirectory(projectName);
        string projectFile = Path.Combine(projectDirectory, projectName + ".csproj");
        string projectContent = File.ReadAllText(projectFile);

        Assert.Multiple(() =>
        {
            Assert.That(projectContent, Does.Not.Contain("Microsoft.AspNetCore.App"));
            Assert.That(projectContent, Does.Not.Contain("AventusSharp.AspNetCore"));
        });

        string[] sourceFiles = Directory.GetFiles(
            projectDirectory,
            "*.cs",
            SearchOption.AllDirectories);
        string[] forbiddenFiles = sourceFiles
            .Where(file => File.ReadAllText(file).Contains("Microsoft.AspNetCore"))
            .Select(file => Path.GetRelativePath(projectDirectory, file))
            .ToArray();

        Assert.That(forbiddenFiles, Is.Empty,
            $"{projectName} must remain independent from ASP.NET Core.");
    }

    private static string FindProjectDirectory(string projectName)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, projectName);
            if (File.Exists(Path.Combine(candidate, projectName + ".csproj")))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException($"Unable to find {projectName}.");
    }
}
