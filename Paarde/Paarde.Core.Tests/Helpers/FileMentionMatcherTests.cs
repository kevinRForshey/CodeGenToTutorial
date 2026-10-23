using Paarde.Core.Helpers;
using Paarde.Core.Models;

namespace Paarde.Core.Tests.Helpers;

public class FileMentionMatcherTests
{
    private static FileTreeEntry BuildFakeTree() => new(
        Name: "root",
        FullPath: "/work",
        IsDirectory: true,
        Children:
        [
            new FileTreeEntry("Program.cs", "/work/Program.cs", false, []),
            new FileTreeEntry("README.md", "/work/README.md", false, []),
            new FileTreeEntry("src", "/work/src", true,
            [
                new FileTreeEntry("Foo.cs", "/work/src/Foo.cs", false, []),
                new FileTreeEntry("Bar.cs", "/work/src/Bar.cs", false, []),
            ]),
        ]);

    [Fact]
    public void FindMatches_EmptyQuery_ReturnsEveryFileSortedAlphabetically()
    {
        var matches = FileMentionMatcher.FindMatches(BuildFakeTree(), query: "");

        Assert.Equal(["Program.cs", "README.md", "src/Bar.cs", "src/Foo.cs"], matches);
    }

    [Fact]
    public void FindMatches_QueryMatchingFileName_ReturnsOnlyThatFile()
    {
        var matches = FileMentionMatcher.FindMatches(BuildFakeTree(), query: "Foo");

        Assert.Equal(["src/Foo.cs"], matches);
    }

    [Fact]
    public void FindMatches_QueryMatchingADirectoryPrefix_ReturnsEveryFileUnderIt()
    {
        var matches = FileMentionMatcher.FindMatches(BuildFakeTree(), query: "src/");

        Assert.Equal(["src/Bar.cs", "src/Foo.cs"], matches);
    }

    [Fact]
    public void FindMatches_QueryIsCaseInsensitive()
    {
        var matches = FileMentionMatcher.FindMatches(BuildFakeTree(), query: "readme");

        Assert.Equal(["README.md"], matches);
    }

    [Fact]
    public void FindMatches_NoFileMatches_ReturnsEmpty()
    {
        var matches = FileMentionMatcher.FindMatches(BuildFakeTree(), query: "nonexistent");

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_MoreMatchesThanMaxResults_IsTruncated()
    {
        var matches = FileMentionMatcher.FindMatches(BuildFakeTree(), query: "", maxResults: 2);

        Assert.Equal(["Program.cs", "README.md"], matches);
    }
}
