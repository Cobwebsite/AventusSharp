using System.Text;
using AventusSharp.Routes;
using AventusSharp.Routes.Request;
using NUnit.Framework;

namespace AventusSharpTest.Routes;

[TestFixture]
[NonParallelizable]
public sealed class HttpFileTests
{
    [Test]
    public void MoveWithError_moves_content_updates_path_and_creates_parent_directory()
    {
        var source = NewPath("move-source");
        var destination = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"http-file-dir-{Guid.NewGuid():N}",
            "move-destination.txt");
        File.WriteAllText(source, "move content", Encoding.UTF8);
        var file = new HttpFile(
            "file", "source.txt", source, "text/plain");

        try
        {
            var result = file.MoveWithError(destination);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True,
                    ErrorMessages(result.Errors));
                Assert.That(result.Result, Is.True);
                Assert.That(file.FilePath, Is.EqualTo(destination));
                Assert.That(File.Exists(source), Is.False);
                Assert.That(File.ReadAllText(destination, Encoding.UTF8),
                    Is.EqualTo("move content"));
            });
        }
        finally
        {
            DeleteIfPresent(source);
            DeleteIfPresent(destination);
        }
    }

    [Test]
    public void CopyWithError_copies_content_and_updates_the_current_path()
    {
        var source = NewPath("copy-source");
        var destination = NewPath("copy-destination");
        File.WriteAllText(source, "copy content", Encoding.UTF8);
        var file = new HttpFile(
            "file", "source.txt", source, "text/plain");

        try
        {
            var result = file.CopyWithError(destination);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True,
                    ErrorMessages(result.Errors));
                Assert.That(result.Result, Is.True);
                Assert.That(file.FilePath, Is.EqualTo(destination));
                Assert.That(File.ReadAllText(source, Encoding.UTF8),
                    Is.EqualTo("copy content"));
                Assert.That(File.ReadAllText(destination, Encoding.UTF8),
                    Is.EqualTo("copy content"));
            });
        }
        finally
        {
            DeleteIfPresent(source);
            DeleteIfPresent(destination);
        }
    }

    [Test]
    public void Missing_source_returns_CantMoveFile_and_preserves_current_path()
    {
        var source = NewPath("missing-source");
        var destination = NewPath("missing-destination");
        var file = new HttpFile(
            "file", "missing.txt", source, "text/plain");

        var result = file.MoveWithError(destination);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Result, Is.False);
            Assert.That(result.Errors.Select(error => error.Code),
                Does.Contain(RouteErrorCode.CantMoveFile));
            Assert.That(file.FilePath, Is.EqualTo(source));
            Assert.That(File.Exists(destination), Is.False);
        });
    }

    private static string NewPath(string prefix) => Path.Combine(
        TestContext.CurrentContext.WorkDirectory,
        $"{prefix}-{Guid.NewGuid():N}.txt");

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string ErrorMessages(
        IEnumerable<AventusSharp.Tools.GenericError> errors) =>
        string.Join(Environment.NewLine,
            errors.Select(error => error.Message));
}
