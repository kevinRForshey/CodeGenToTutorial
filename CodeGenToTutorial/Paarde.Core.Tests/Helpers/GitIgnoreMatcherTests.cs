using Paarde.Core.Helpers;

namespace Paarde.Core.Tests.Helpers;

public class GitIgnoreMatcherTests : IDisposable
{
    private readonly string _workingDirectory;

    public GitIgnoreMatcherTests()
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

    private void WriteGitIgnore(string content) =>
        File.WriteAllText(Path.Combine(_workingDirectory, ".gitignore"), content);

    [Fact]
    public void IsIgnored_NoGitIgnoreFile_ReturnsFalse()
    {
        Assert.False(GitIgnoreMatcher.IsIgnored(_workingDirectory, "anything.txt"));
    }

    [Fact]
    public void IsIgnored_ExactFileNameMatch_ReturnsTrue()
    {
        WriteGitIgnore("secrets.txt");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "secrets.txt"));
    }

    [Fact]
    public void IsIgnored_NoMatchingPattern_ReturnsFalse()
    {
        WriteGitIgnore("secrets.txt");

        Assert.False(GitIgnoreMatcher.IsIgnored(_workingDirectory, "other.txt"));
    }

    [Fact]
    public void IsIgnored_CommentsAndBlankLines_AreSkipped()
    {
        WriteGitIgnore("# comment\n\n   \nsecrets.txt");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "secrets.txt"));
        Assert.False(GitIgnoreMatcher.IsIgnored(_workingDirectory, "# comment"));
    }

    [Fact]
    public void IsIgnored_WildcardExtension_MatchesAnyFileWithThatExtension()
    {
        WriteGitIgnore("*.log");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "output.log"));
        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "nested/output.log"));
        Assert.False(GitIgnoreMatcher.IsIgnored(_workingDirectory, "output.txt"));
    }

    [Fact]
    public void IsIgnored_DirectoryPattern_IgnoresEverythingUnderIt()
    {
        WriteGitIgnore("bin/");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "bin/Debug/app.dll"));
        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "src/bin/Debug/app.dll"));
    }

    [Fact]
    public void IsIgnored_RootAnchoredPattern_OnlyMatchesAtRoot()
    {
        WriteGitIgnore("/build.txt");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "build.txt"));
        Assert.False(GitIgnoreMatcher.IsIgnored(_workingDirectory, "nested/build.txt"));
    }

    [Fact]
    public void IsIgnored_DoubleStarWildcard_MatchesAcrossDirectories()
    {
        WriteGitIgnore("logs/**/*.log");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "logs/a/b/output.log"));
    }

    [Fact]
    public void IsIgnored_QuestionMarkWildcard_MatchesSingleCharacter()
    {
        WriteGitIgnore("file?.txt");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "file1.txt"));
        Assert.False(GitIgnoreMatcher.IsIgnored(_workingDirectory, "file12.txt"));
    }

    [Fact]
    public void IsIgnored_NegationAfterBroaderIgnore_UnIgnoresTheNegatedFile()
    {
        WriteGitIgnore("*.log\n!keep.log");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "output.log"));
        Assert.False(GitIgnoreMatcher.IsIgnored(_workingDirectory, "keep.log"));
    }

    [Fact]
    public void IsIgnored_LaterPatternOverridesEarlierOne_LastMatchWins()
    {
        WriteGitIgnore("!important.txt\nimportant.txt");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "important.txt"));
    }

    [Fact]
    public void IsIgnored_BackslashPathSeparator_IsNormalizedToForwardSlash()
    {
        WriteGitIgnore("bin/");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "bin\\Debug\\app.dll"));
    }

    [Fact]
    public void IsIgnored_BareNegationMarkerWithNothingAfterIt_IsSkippedWithoutMatching()
    {
        // "!" alone becomes an empty pattern once the negation marker is stripped - there's nothing
        // left to match against, so this line should be a no-op rather than negating everything.
        WriteGitIgnore("!\nsecrets.txt");

        Assert.True(GitIgnoreMatcher.IsIgnored(_workingDirectory, "secrets.txt"));
    }
}
