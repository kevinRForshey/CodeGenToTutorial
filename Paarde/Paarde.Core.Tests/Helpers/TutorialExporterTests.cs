using Paarde.Core.Helpers;
using Paarde.Core.ViewModels;

namespace Paarde.Core.Tests.Helpers;

public class TutorialExporterTests
{
    private static ProposedFileChangeViewModel CreateFile(string filePath, params ProposedHunk[] hunks) =>
        new(filePath, "proposed") { Hunks = hunks };

    [Fact]
    public void BuildMarkdown_IncludesOverviewAndFileHeading()
    {
        var files = new[] { CreateFile("src/Foo.cs") };

        var markdown = TutorialExporter.BuildMarkdown("This tutorial adds logging.", files);

        Assert.Contains("# Tutorial", markdown);
        Assert.Contains("This tutorial adds logging.", markdown);
        Assert.Contains("## src/Foo.cs", markdown);
    }

    [Fact]
    public void BuildMarkdown_NoOverview_OmitsOverviewSection()
    {
        var markdown = TutorialExporter.BuildMarkdown(string.Empty, []);

        Assert.Equal("# Tutorial\n", markdown);
    }

    [Fact]
    public void BuildMarkdown_NumbersStepsAndIncludesTitleAndExplanation()
    {
        var files = new[]
        {
            CreateFile(
                "src/Foo.cs",
                new ProposedHunk("Add a helper", "Explains the helper.", "snippet", null),
                new ProposedHunk("Wire it up", "Explains wiring.", "snippet", null)),
        };

        var markdown = TutorialExporter.BuildMarkdown(string.Empty, files);

        Assert.Contains("### Step 1: Add a helper", markdown);
        Assert.Contains("Explains the helper.", markdown);
        Assert.Contains("### Step 2: Wire it up", markdown);
        Assert.Contains("Explains wiring.", markdown);
        Assert.True(markdown.IndexOf("Step 1", StringComparison.Ordinal) < markdown.IndexOf("Step 2", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildHtml_IsAWellFormedDocumentWithEscapedContent()
    {
        var files = new[]
        {
            CreateFile("src/<Foo>.cs", new ProposedHunk("Add <a> helper", "Uses `DoThing()` & **bold**.", "snippet", null)),
        };

        var html = TutorialExporter.BuildHtml("Overview with <tags> & stuff.", files);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<h1>Tutorial</h1>", html);
        Assert.Contains("Overview with &lt;tags&gt; &amp; stuff.", html);
        Assert.Contains("<h2>src/&lt;Foo&gt;.cs</h2>", html);
        Assert.Contains("<h3>Step 1: Add &lt;a&gt; helper</h3>", html);
        Assert.Contains("<code>DoThing()</code>", html);
        Assert.Contains("<strong>bold</strong>", html);
        Assert.DoesNotContain("<a> helper", html);
    }

    [Fact]
    public void BuildHtml_ListItems_AreWrappedInUlLi()
    {
        var files = new[]
        {
            CreateFile("src/Foo.cs", new ProposedHunk("Step with a list", "- first\n- second", "snippet", null)),
        };

        var html = TutorialExporter.BuildHtml(string.Empty, files);

        Assert.Contains("<ul>", html);
        Assert.Contains("<li>first</li>", html);
        Assert.Contains("<li>second</li>", html);
        Assert.Contains("</ul>", html);
    }
}
