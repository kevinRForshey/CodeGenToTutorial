using Paarde.Core.Helpers;

namespace Paarde.Core.Tests.Helpers;

public class ProtectedPathCheckerTests : IDisposable
{
    private readonly string _workingDirectory;

    public ProtectedPathCheckerTests()
    {
        _workingDirectory = Directory.CreateTempSubdirectory("PaardeTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(".git/config")]
    [InlineData("bin/Debug/app.dll")]
    [InlineData("obj/Debug/app.cache")]
    [InlineData("src/bin/Debug/app.dll")]
    [InlineData("nested/.git/HEAD")]
    public void IsProtected_StructuralProtectedSegment_ReturnsTrue(string relativePath)
    {
        Assert.True(ProtectedPathChecker.IsProtected(_workingDirectory, relativePath));
    }

    [Fact]
    public void IsProtected_OrdinaryFile_ReturnsFalse()
    {
        Assert.False(ProtectedPathChecker.IsProtected(_workingDirectory, "src/Program.cs"));
    }

    [Fact]
    public void IsProtected_FileNameHappensToMatchProtectedSegment_ReturnsFalse()
    {
        // "bin" as the final path segment is the file itself, not a directory it's inside - only a
        // directory *named* bin/obj/.git along the way should be treated as protected.
        Assert.False(ProtectedPathChecker.IsProtected(_workingDirectory, "src/bin"));
    }

    [Fact]
    public void IsProtected_FileMatchedByGitIgnore_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, ".gitignore"), "*.log");

        Assert.True(ProtectedPathChecker.IsProtected(_workingDirectory, "output.log"));
    }

    [Fact]
    public void IsProtected_NoGitIgnoreAndNoStructuralMatch_ReturnsFalse()
    {
        Assert.False(ProtectedPathChecker.IsProtected(_workingDirectory, "notes.txt"));
    }
}
