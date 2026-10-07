using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Tests.Helpers;

public class HunkLocationResolverTests
{
    [Fact]
    public void Resolve_SnippetAtStartOfFile_ReturnsMatchingLineRange()
    {
        const string file = "line one\nline two\nline three";
        const string snippet = "line one";

        var location = HunkLocationResolver.Resolve(file, snippet);

        Assert.Equal(new HunkLocation(0, 0), location);
    }

    [Fact]
    public void Resolve_MultiLineSnippetInMiddle_ReturnsMatchingLineRange()
    {
        const string file = "a\nb\nc\nd\ne";
        const string snippet = "b\nc\nd";

        var location = HunkLocationResolver.Resolve(file, snippet);

        Assert.Equal(new HunkLocation(1, 3), location);
    }

    [Fact]
    public void Resolve_SnippetNotPresent_ReturnsNull()
    {
        const string file = "a\nb\nc";
        const string snippet = "x\ny";

        var location = HunkLocationResolver.Resolve(file, snippet);

        Assert.Null(location);
    }

    [Fact]
    public void Resolve_EmptySnippet_ReturnsNull()
    {
        var location = HunkLocationResolver.Resolve("a\nb\nc", string.Empty);

        Assert.Null(location);
    }

    [Fact]
    public void Resolve_SnippetLongerThanFile_ReturnsNull()
    {
        var location = HunkLocationResolver.Resolve("a\nb", "a\nb\nc");

        Assert.Null(location);
    }

    [Fact]
    public void Resolve_CrLfLineEndings_StillMatches()
    {
        const string file = "a\r\nb\r\nc";
        const string snippet = "b";

        var location = HunkLocationResolver.Resolve(file, snippet);

        Assert.Equal(new HunkLocation(1, 1), location);
    }
}
