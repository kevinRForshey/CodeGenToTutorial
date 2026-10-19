using Paarde.Core.Helpers;

namespace Paarde.Core.Tests.Helpers;

public class FileTreeBuilderTests : IDisposable
{
    private readonly string _workingDirectory;

    public FileTreeBuilderTests()
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

    private string CreateFile(string relativePath)
    {
        var fullPath = Path.Combine(_workingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "content");
        return fullPath;
    }

    [Fact]
    public void Build_EmptyDirectory_ReturnsADirectoryWithNoChildren()
    {
        var root = FileTreeBuilder.Build(_workingDirectory);

        Assert.True(root.IsDirectory);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Build_FilesAndSubdirectories_ListsDirectoriesBeforeFilesBothAlphabetically()
    {
        CreateFile("b.txt");
        CreateFile("a.txt");
        CreateFile("ZDir/nested.txt");
        CreateFile("ADir/nested.txt");

        var root = FileTreeBuilder.Build(_workingDirectory);

        Assert.Equal(["ADir", "ZDir", "a.txt", "b.txt"], root.Children.Select(c => c.Name));
    }

    [Fact]
    public void Build_NestedDirectory_HasItsOwnChildren()
    {
        CreateFile("src/Program.cs");

        var root = FileTreeBuilder.Build(_workingDirectory);

        var srcDirectory = Assert.Single(root.Children);
        Assert.True(srcDirectory.IsDirectory);
        var file = Assert.Single(srcDirectory.Children);
        Assert.Equal("Program.cs", file.Name);
        Assert.False(file.IsDirectory);
    }

    [Fact]
    public void Build_GitDirectory_IsAlwaysExcluded()
    {
        CreateFile(".git/HEAD");

        var root = FileTreeBuilder.Build(_workingDirectory);

        Assert.Empty(root.Children);
    }

    [Fact]
    public void Build_PathMatchedByRootGitIgnore_IsExcluded()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, ".gitignore"), "[Bb]in/\n*.log");
        CreateFile("bin/app.dll");
        CreateFile("output.log");
        CreateFile("Program.cs");

        var root = FileTreeBuilder.Build(_workingDirectory);

        Assert.Equal([".gitignore", "Program.cs"], root.Children.Select(c => c.Name));
    }

    [Fact]
    public void Build_FileFullPath_IsTheAbsolutePathOnDisk()
    {
        var expectedPath = CreateFile("a.txt");

        var root = FileTreeBuilder.Build(_workingDirectory);

        Assert.Equal(expectedPath, root.Children.Single().FullPath);
    }
}
