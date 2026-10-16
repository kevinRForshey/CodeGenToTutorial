using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Tests.Helpers;

public class ClaudeJsonResponseParserTests
{
    [Fact]
    public void TryParse_AnswerShape_ReturnsAnswerAsTutorialWithNoFiles()
    {
        const string json = """{"responseType": "answer", "answer": "4"}""";

        var success = ClaudeJsonResponseParser.TryParse(json, out var response, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("4", response!.Tutorial);
        Assert.Empty(response.Files);
    }

    [Fact]
    public void TryParse_DevelopmentShapeWithFileAndStep_ParsesEverything()
    {
        const string json = """
            {
              "responseType": "development",
              "tutorial": "Adds a Foo class.",
              "files": [
                {
                  "path": "src/Foo.cs",
                  "content": "public class Foo {}",
                  "steps": [
                    { "title": "Add Foo class", "explanation": "This step adds the Foo class.", "snippet": "public class Foo {}" }
                  ]
                }
              ]
            }
            """;

        var success = ClaudeJsonResponseParser.TryParse(json, out var response, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("Adds a Foo class.", response!.Tutorial);
        var file = Assert.Single(response.Files);
        Assert.Equal("src/Foo.cs", file.FilePath);
        Assert.Equal("public class Foo {}", file.ProposedContent);
        var hunk = Assert.Single(file.Hunks);
        Assert.Equal("Add Foo class", hunk.Title);
        Assert.Equal("This step adds the Foo class.", hunk.Explanation);
        Assert.Equal("public class Foo {}", hunk.Snippet);
        Assert.Equal(new HunkLocation(0, 0), hunk.Location);
    }

    [Fact]
    public void TryParse_DevelopmentShapeWithNoFiles_ReturnsEmptyFileList()
    {
        const string json = """{"responseType": "development", "tutorial": "No files need changes.", "files": []}""";

        var success = ClaudeJsonResponseParser.TryParse(json, out var response, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Empty(response!.Files);
    }

    [Fact]
    public void TryParse_DevelopmentShapeWithMissingFilesKey_TreatsAsEmptyFileList()
    {
        const string json = """{"responseType": "development", "tutorial": "No files need changes."}""";

        var success = ClaudeJsonResponseParser.TryParse(json, out var response, out var error);

        Assert.True(success);
        Assert.Empty(response!.Files);
    }

    [Fact]
    public void TryParse_JsonWrappedInCommentaryAndFence_StillExtractsTheObject()
    {
        const string text = """
            Sure, here's the JSON:
            ```json
            {"responseType": "answer", "answer": "4"}
            ```
            Hope that helps!
            """;

        var success = ClaudeJsonResponseParser.TryParse(text, out var response, out var error);

        Assert.True(success);
        Assert.Equal("4", response!.Tutorial);
    }

    [Fact]
    public void TryParse_FileContentContainingBracesAndFences_DoesNotTruncateEarly()
    {
        // Regression check for the markdown parser's known nested-fence fragility (see roadmap
        // "Protocol fragility"): since file content is a JSON string, braces/backtick-fences inside it
        // are just characters inside a quoted value and must not end the outer object early.
        const string json = """
            {
              "responseType": "development",
              "tutorial": "Adds a README with a code sample.",
              "files": [
                { "path": "README.md", "content": "Example:\n```\nif (x) { y(); }\n```\n", "steps": [] }
              ]
            }
            """;

        var success = ClaudeJsonResponseParser.TryParse(json, out var response, out var error);

        Assert.True(success);
        var file = Assert.Single(response!.Files);
        Assert.Equal("Example:\n```\nif (x) { y(); }\n```\n", file.ProposedContent);
    }

    [Fact]
    public void TryParse_NoJsonObjectInText_ReturnsFalseWithError()
    {
        var success = ClaudeJsonResponseParser.TryParse("This is just plain text with no braces at all.", out var response, out var error);

        Assert.False(success);
        Assert.Null(response);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_UnterminatedJsonObject_ReturnsFalseWithError()
    {
        var success = ClaudeJsonResponseParser.TryParse("{\"responseType\": \"answer\", \"answer\": \"4\"", out var response, out var error);

        Assert.False(success);
        Assert.Null(response);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_MissingResponseType_ReturnsFalseWithError()
    {
        var success = ClaudeJsonResponseParser.TryParse("""{"answer": "4"}""", out var response, out var error);

        Assert.False(success);
        Assert.Contains("responseType", error);
    }

    [Fact]
    public void TryParse_UnrecognizedResponseType_ReturnsFalseWithError()
    {
        var success = ClaudeJsonResponseParser.TryParse("""{"responseType": "banana"}""", out var response, out var error);

        Assert.False(success);
        Assert.Contains("responseType", error);
    }

    [Fact]
    public void TryParse_AnswerShapeWithMissingAnswer_ReturnsFalseWithError()
    {
        var success = ClaudeJsonResponseParser.TryParse("""{"responseType": "answer"}""", out var response, out var error);

        Assert.False(success);
        Assert.Contains("answer", error);
    }

    [Fact]
    public void TryParse_DevelopmentShapeWithMissingTutorial_ReturnsFalseWithError()
    {
        var success = ClaudeJsonResponseParser.TryParse("""{"responseType": "development", "files": []}""", out var response, out var error);

        Assert.False(success);
        Assert.Contains("tutorial", error);
    }

    [Fact]
    public void TryParse_FileMissingPath_ReturnsFalseWithError()
    {
        const string json = """{"responseType": "development", "tutorial": "t", "files": [{"content": "x"}]}""";

        var success = ClaudeJsonResponseParser.TryParse(json, out var response, out var error);

        Assert.False(success);
        Assert.Contains("path", error);
    }

    [Fact]
    public void TryParse_FileMissingContent_ReturnsFalseWithError()
    {
        const string json = """{"responseType": "development", "tutorial": "t", "files": [{"path": "a.txt"}]}""";

        var success = ClaudeJsonResponseParser.TryParse(json, out var response, out var error);

        Assert.False(success);
        Assert.Contains("content", error);
    }

    [Fact]
    public void TryParse_StepMissingTitle_ReturnsFalseWithError()
    {
        const string json = """
            {"responseType": "development", "tutorial": "t", "files": [
              {"path": "a.txt", "content": "x", "steps": [{"explanation": "e", "snippet": "x"}]}
            ]}
            """;

        var success = ClaudeJsonResponseParser.TryParse(json, out var response, out var error);

        Assert.False(success);
        Assert.Contains("title", error);
    }

    [Fact]
    public void TryParse_MalformedJsonSyntax_ReturnsFalseWithError()
    {
        var success = ClaudeJsonResponseParser.TryParse("""{"responseType": "answer", "answer": }""", out var response, out var error);

        Assert.False(success);
        Assert.Null(response);
        Assert.NotNull(error);
    }
}
