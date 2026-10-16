using CodeGenToTutorial.Core.Helpers;

namespace CodeGenToTutorial.Core.Tests.Helpers;

public class StructuredResponseParserTests
{
    [Fact]
    public void Parse_ValidJson_DoesNotUseFallbackAndHasNoValidationError()
    {
        const string json = """{"responseType": "answer", "answer": "4"}""";

        var result = StructuredResponseParser.Parse(json);

        Assert.False(result.UsedFallback);
        Assert.Null(result.ValidationError);
        Assert.Equal("4", result.Response.Tutorial);
    }

    [Fact]
    public void Parse_LegacyMarkdownResponse_FallsBackAndReportsTheJsonValidationError()
    {
        const string markdown = """
            ## Tutorial
            Adds a Foo class.

            ## Files
            ### File: src/Foo.cs
            ```
            public class Foo {}
            ```
            """;

        var result = StructuredResponseParser.Parse(markdown);

        Assert.True(result.UsedFallback);
        Assert.NotNull(result.ValidationError);
        Assert.Equal("Adds a Foo class.", result.Response.Tutorial);
        Assert.Single(result.Response.Files);
    }

    [Fact]
    public void Parse_UnstructuredPlainText_FallsBackToTreatingItAsAnAnswer()
    {
        const string text = "This is a plain answer with no JSON and no markdown headers.";

        var result = StructuredResponseParser.Parse(text);

        Assert.True(result.UsedFallback);
        Assert.Equal(text, result.Response.Tutorial);
        Assert.Empty(result.Response.Files);
    }
}
