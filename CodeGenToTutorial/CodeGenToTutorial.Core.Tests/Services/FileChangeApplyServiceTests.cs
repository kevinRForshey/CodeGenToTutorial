using CodeGenToTutorial.Core.Services;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Tests.Services;

public class FileChangeApplyServiceTests : IDisposable
{
    private readonly string _workingDirectory;
    private readonly FileChangeApplyService _service = new();

    public FileChangeApplyServiceTests()
    {
        _workingDirectory = Directory.CreateTempSubdirectory("CodeGenToTutorialTests_").FullName;
    }

    public void Dispose()
    {
        if (!Directory.Exists(_workingDirectory))
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            // Restore write access in case the permission-failure test locked it down, so cleanup
            // doesn't itself throw.
            File.SetUnixFileMode(_workingDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Directory.Delete(_workingDirectory, recursive: true);
    }

    [Fact]
    public void Apply_NoWorkingDirectory_Throws()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "hi");

        Assert.Throws<InvalidOperationException>(() => _service.Apply(file, string.Empty));
    }

    [Fact]
    public void Apply_NewFile_WritesContentToWorkingDirectory()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "hello world");

        _service.Apply(file, _workingDirectory);

        var written = Path.Combine(_workingDirectory, "a.txt");
        Assert.True(File.Exists(written));
        Assert.Equal("hello world", File.ReadAllText(written));
    }

    [Fact]
    public void Apply_NestedPath_CreatesIntermediateDirectories()
    {
        var file = new ProposedFileChangeViewModel(Path.Combine("src", "nested", "Foo.cs"), "class Foo {}");

        _service.Apply(file, _workingDirectory);

        var written = Path.Combine(_workingDirectory, "src", "nested", "Foo.cs");
        Assert.True(File.Exists(written));
        Assert.Equal("class Foo {}", File.ReadAllText(written));
    }

    [Fact]
    public void Apply_OverwritesExistingFile()
    {
        var path = Path.Combine(_workingDirectory, "a.txt");
        File.WriteAllText(path, "old content");
        var file = new ProposedFileChangeViewModel("a.txt", "new content");

        _service.Apply(file, _workingDirectory);

        Assert.Equal("new content", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("../../../../etc/escape.txt")]
    [InlineData("nested/../../escape.txt")]
    public void Apply_RelativePathTraversal_ThrowsAndDoesNotWrite(string traversalPath)
    {
        var file = new ProposedFileChangeViewModel(traversalPath, "malicious");

        Assert.Throws<InvalidOperationException>(() => _service.Apply(file, _workingDirectory));

        var escapedTarget = Path.GetFullPath(Path.Combine(_workingDirectory, traversalPath));
        Assert.False(File.Exists(escapedTarget));
    }

    [Fact]
    public void Apply_AbsolutePath_ThrowsAndDoesNotWrite()
    {
        var outsideDirectory = Directory.CreateTempSubdirectory("CodeGenToTutorialTests_outside_").FullName;
        try
        {
            var absoluteTarget = Path.Combine(outsideDirectory, "escape.txt");
            var file = new ProposedFileChangeViewModel(absoluteTarget, "malicious");

            Assert.Throws<InvalidOperationException>(() => _service.Apply(file, _workingDirectory));

            Assert.False(File.Exists(absoluteTarget));
        }
        finally
        {
            Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    [Fact]
    public void Apply_DestinationDirectoryNotWritable_ThrowsIOOrUnauthorizedException()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            // Windows ACLs and root both make a reliable "permission denied" repro impractical here;
            // this path is covered on a normal Unix CI/dev user.
            return;
        }

        File.SetUnixFileMode(_workingDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var file = new ProposedFileChangeViewModel("a.txt", "hi");

        Assert.ThrowsAny<UnauthorizedAccessException>(() => _service.Apply(file, _workingDirectory));
    }
}
