using CodeGenToTutorial.Core.Helpers;

namespace CodeGenToTutorial.Core.Tests.Helpers;

public class ExecutablePathResolverTests
{
    [Fact]
    public void ExistsOnPath_ExecutableInOneOfSeveralDirectories_ReturnsTrue()
    {
        var emptyDirectory = Directory.CreateTempSubdirectory();
        var executableDirectory = Directory.CreateTempSubdirectory();
        File.WriteAllText(Path.Combine(executableDirectory.FullName, "mytool"), string.Empty);
        var pathVariable = string.Join(Path.PathSeparator, emptyDirectory.FullName, executableDirectory.FullName);

        try
        {
            Assert.True(ExecutablePathResolver.ExistsOnPath("mytool", pathVariable));
        }
        finally
        {
            emptyDirectory.Delete(recursive: true);
            executableDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ExistsOnPath_ExecutableNotInAnyDirectory_ReturnsFalse()
    {
        var emptyDirectory = Directory.CreateTempSubdirectory();

        try
        {
            Assert.False(ExecutablePathResolver.ExistsOnPath("mytool", emptyDirectory.FullName));
        }
        finally
        {
            emptyDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ExistsOnPath_EmptyPathVariable_ReturnsFalse()
    {
        Assert.False(ExecutablePathResolver.ExistsOnPath("mytool", string.Empty));
    }

    [Fact]
    public void ExistsOnPath_PathVariableWithBlankEntries_SkipsThemWithoutThrowing()
    {
        var executableDirectory = Directory.CreateTempSubdirectory();
        File.WriteAllText(Path.Combine(executableDirectory.FullName, "mytool"), string.Empty);
        var pathVariable = string.Join(Path.PathSeparator, string.Empty, executableDirectory.FullName, string.Empty);

        try
        {
            Assert.True(ExecutablePathResolver.ExistsOnPath("mytool", pathVariable));
        }
        finally
        {
            executableDirectory.Delete(recursive: true);
        }
    }
}
