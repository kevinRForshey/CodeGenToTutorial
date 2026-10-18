using Paarde.Core.Helpers;
using Paarde.Core.Models;

namespace Paarde.Core.Tests.Helpers;

public class SearchReplaceEditApplierTests
{
    private static ClaudeJsonEdit Edit(string search, string replace, string title = "Step") =>
        new() { Title = title, Search = search, Replace = replace };

    [Fact]
    public void Apply_ExactMatch_ReplacesTheMatchedLines()
    {
        const string original = "line1\nline2\nline3";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("line2", "replaced")]);

        Assert.Equal("line1\nreplaced\nline3", result.Content);
        var outcome = Assert.Single(result.Outcomes);
        Assert.True(outcome.Success);
        Assert.Equal(new HunkLocation(1, 1), outcome.Location);
    }

    [Fact]
    public void Apply_MultiLineSearchAndReplace_ReplacesTheWholeRange()
    {
        const string original = "a\nb\nc\nd";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("b\nc", "x\ny\nz")]);

        Assert.Equal("a\nx\ny\nz\nd", result.Content);
        Assert.Equal(new HunkLocation(1, 3), result.Outcomes[0].Location);
    }

    [Fact]
    public void Apply_MultipleEditsInOrder_AppliesEachAgainstThePreviousResult()
    {
        const string original = "a\nb\nc";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("a", "1"), Edit("c", "3")]);

        Assert.Equal("1\nb\n3", result.Content);
        Assert.All(result.Outcomes, o => Assert.True(o.Success));
    }

    [Fact]
    public void Apply_SearchDiffersOnlyByIndentation_StillMatchesViaTrimmedFallback()
    {
        const string original = "    line1\n    line2";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("line1", "replaced")]);

        Assert.True(result.Outcomes[0].Success);
        Assert.Equal("replaced\n    line2", result.Content);
    }

    [Fact]
    public void Apply_SearchDiffersByInternalWhitespace_StillMatchesViaCollapsedFallback()
    {
        const string original = "if  (x)   {\n   y();\n}";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("if (x) {", "if (x) { /* changed */")]);

        Assert.True(result.Outcomes[0].Success);
    }

    [Fact]
    public void Apply_SearchTextNotFound_ReportsFailureAndLeavesContentUnchanged()
    {
        const string original = "a\nb\nc";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("nope", "x")]);

        Assert.Equal(original, result.Content);
        Assert.False(result.Outcomes[0].Success);
        Assert.Null(result.Outcomes[0].Location);
    }

    [Fact]
    public void Apply_SearchTextMatchesMoreThanOnce_ReportsFailureRatherThanGuessing()
    {
        const string original = "a\nb\na\nb";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("a", "x")]);

        Assert.Equal(original, result.Content);
        Assert.False(result.Outcomes[0].Success);
    }

    [Fact]
    public void Apply_EmptyReplace_DeletesTheSearchTextAndLocationIsNull()
    {
        const string original = "a\nb\nc";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("b", "")]);

        Assert.Equal("a\nc", result.Content);
        Assert.True(result.Outcomes[0].Success);
        Assert.Null(result.Outcomes[0].Location);
    }

    [Fact]
    public void Apply_NoEdits_ReturnsOriginalContentUnchanged()
    {
        const string original = "a\nb\nc";

        var result = SearchReplaceEditApplier.Apply(original, []);

        Assert.Equal(original, result.Content);
        Assert.Empty(result.Outcomes);
    }

    [Fact]
    public void Apply_NullSearch_TreatsAsEmptyAndReportsFailure()
    {
        const string original = "a\nb\nc";

        var result = SearchReplaceEditApplier.Apply(original, [new ClaudeJsonEdit { Title = "x", Search = null, Replace = "y" }]);

        Assert.Equal(original, result.Content);
        Assert.False(result.Outcomes[0].Success);
    }

    [Fact]
    public void Apply_NullReplace_TreatsAsEmptyDeletion()
    {
        const string original = "a\nb\nc";

        var result = SearchReplaceEditApplier.Apply(original, [new ClaudeJsonEdit { Title = "x", Search = "b", Replace = null }]);

        Assert.Equal("a\nc", result.Content);
        Assert.True(result.Outcomes[0].Success);
    }

    [Fact]
    public void Apply_SearchLongerThanContent_ReportsFailure()
    {
        const string original = "a";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("a\nb", "x")]);

        Assert.False(result.Outcomes[0].Success);
    }

    [Fact]
    public void Apply_LaterEditFailsAfterAnEarlierOneSucceeds_EarlierChangeIsStillInTheResult()
    {
        const string original = "a\nb\nc";

        var result = SearchReplaceEditApplier.Apply(original, [Edit("a", "1"), Edit("nope", "x")]);

        Assert.Equal("1\nb\nc", result.Content);
        Assert.True(result.Outcomes[0].Success);
        Assert.False(result.Outcomes[1].Success);
    }
}
