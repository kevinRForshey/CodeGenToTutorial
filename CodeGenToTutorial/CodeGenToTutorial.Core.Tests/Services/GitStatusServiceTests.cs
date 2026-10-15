using System.Diagnostics;

using CodeGenToTutorial.Core.Services;

namespace CodeGenToTutorial.Core.Tests.Services;

public class GitStatusServiceTests : IDisposable
{
    private readonly string _workingDirectory;
    private readonly GitStatusService _service = new();

    public GitStatusServiceTests()
    {
        _workingDirectory = Directory.CreateTempSubdirectory("CodeGenToTutorialTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_NotAGitRepository_ReturnsNotARepository()
    {
        var status = _service.GetStatus(_workingDirectory);

        Assert.False(status.IsRepository);
        Assert.False(status.IsDirty);
        Assert.Empty(status.DirtyFiles);
    }

    [Fact]
    public void GetStatus_MissingDirectory_ReturnsNotARepository()
    {
        var status = _service.GetStatus(Path.Combine(_workingDirectory, "does-not-exist"));

        Assert.False(status.IsRepository);
    }

    [Fact]
    public void GetStatus_NullOrWhitespacePath_ReturnsNotARepository()
    {
        Assert.False(_service.GetStatus(string.Empty).IsRepository);
        Assert.False(_service.GetStatus("   ").IsRepository);
    }

    [Fact]
    public void GetStatus_CleanRepository_ReturnsNoDirtyFiles()
    {
        InitRepository();
        File.WriteAllText(Path.Combine(_workingDirectory, "clean.txt"), "committed");
        RunGit("add", "clean.txt");
        CommitAll("initial commit");

        var status = _service.GetStatus(_workingDirectory);

        Assert.True(status.IsRepository);
        Assert.False(status.IsDirty);
        Assert.Empty(status.DirtyFiles);
    }

    [Fact]
    public void GetStatus_ModifiedTrackedFile_ReportsItAsDirty()
    {
        InitRepository();
        File.WriteAllText(Path.Combine(_workingDirectory, "tracked.txt"), "original");
        RunGit("add", "tracked.txt");
        CommitAll("initial commit");

        File.WriteAllText(Path.Combine(_workingDirectory, "tracked.txt"), "changed");

        var status = _service.GetStatus(_workingDirectory);

        Assert.True(status.IsRepository);
        Assert.True(status.IsDirty);
        Assert.Equal(new[] { "tracked.txt" }, status.DirtyFiles);
    }

    [Fact]
    public void GetStatus_UntrackedFile_ReportsItAsDirty()
    {
        InitRepository();
        File.WriteAllText(Path.Combine(_workingDirectory, "new.txt"), "untracked");

        var status = _service.GetStatus(_workingDirectory);

        Assert.True(status.IsRepository);
        Assert.Equal(new[] { "new.txt" }, status.DirtyFiles);
    }

    private void InitRepository() => RunGit("init");

    private void CommitAll(string message) =>
        RunGit("-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "-m", message);

    private void RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed: {process.StandardError.ReadToEnd()}");
        }
    }
}
