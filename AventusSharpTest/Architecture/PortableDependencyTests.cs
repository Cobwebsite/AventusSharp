using NUnit.Framework;

namespace AventusSharpTest.Architecture;

public sealed class PortableDependencyTests
{
    [TestCase("AventusSharp.Core")]
    [TestCase("AventusSharp.Maui")]
    [TestCase("AventusSharp.Data")]
    [TestCase("AventusSharp.Data.Sqlite")]
    [TestCase("AventusSharp.Runtime")]
    [TestCase("AventusSharp.Data.Mysql")]
    [TestCase("AventusSharp.Data.Postgresql")]
    [TestCase("AventusSharp.Data.Mssql")]
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

    [TestCase("AventusSharp.AspNetCore")]
    [TestCase("AventusSharp.Maui")]
    public void Host_packages_do_not_choose_a_database_provider(string projectName)
    {
        string content = ReadProject(projectName);
        Assert.That(content, Does.Not.Contain("AventusSharp.Data.Sqlite"));
        Assert.That(content, Does.Not.Contain("AventusSharp.Data.Mysql"));
        Assert.That(content, Does.Not.Contain("AventusSharp.Data.Postgresql"));
        Assert.That(content, Does.Not.Contain("AventusSharp.Data.Mssql"));
    }

    [TestCase("AventusSharp.Data")]
    [TestCase("AventusSharp.Data.Sqlite")]
    [TestCase("AventusSharp.Data.Mysql")]
    [TestCase("AventusSharp.Data.Postgresql")]
    [TestCase("AventusSharp.Data.Mssql")]
    public void Data_packages_own_their_sources(string projectName)
    {
        Assert.That(ReadProject(projectName), Does.Not.Contain("Compile Include=\".."));
    }

    private static string ReadProject(string projectName)
    {
        string directory = FindProjectDirectory(projectName);
        return File.ReadAllText(Path.Combine(directory, projectName + ".csproj"));
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
