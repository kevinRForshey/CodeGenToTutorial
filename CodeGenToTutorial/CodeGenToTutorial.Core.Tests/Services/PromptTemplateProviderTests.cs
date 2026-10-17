using CodeGenToTutorial.Core.Services;

namespace CodeGenToTutorial.Core.Tests.Services;

public class PromptTemplateProviderTests
{
    [Fact]
    public void CurrentVersion_IsLatestVersion()
    {
        var sut = new PromptTemplateProvider();

        Assert.Equal(PromptTemplateProvider.LatestVersion, sut.CurrentVersion);
    }

    [Fact]
    public void GetTutorialInstructionPrompt_ReturnsTheEmbeddedTemplateContent()
    {
        var sut = new PromptTemplateProvider();

        var prompt = sut.GetTutorialInstructionPrompt();

        Assert.Contains("\"responseType\": \"answer\"", prompt);
        Assert.Contains("\"responseType\": \"development\"", prompt);
        Assert.DoesNotContain("\r", prompt);
    }

    [Fact]
    public void GetTutorialInstructionPrompt_CalledTwice_ReturnsTheSameContent()
    {
        var sut = new PromptTemplateProvider();

        var first = sut.GetTutorialInstructionPrompt();
        var second = sut.GetTutorialInstructionPrompt();

        Assert.Equal(first, second);
    }
}
