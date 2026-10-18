using CodeGenToTutorial.Core.Models;
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

    [Fact]
    public void GetPresetInstruction_General_ReturnsEmpty()
    {
        var sut = new PromptTemplateProvider();

        Assert.Equal(string.Empty, sut.GetPresetInstruction(PromptPreset.General));
    }

    [Theory]
    [InlineData(PromptPreset.Feature)]
    [InlineData(PromptPreset.Bugfix)]
    [InlineData(PromptPreset.Refactor)]
    [InlineData(PromptPreset.Explain)]
    public void GetPresetInstruction_NonGeneralPreset_ReturnsNonEmptyText(PromptPreset preset)
    {
        var sut = new PromptTemplateProvider();

        var instruction = sut.GetPresetInstruction(preset);

        Assert.False(string.IsNullOrWhiteSpace(instruction));
    }

    [Fact]
    public void GetPresetInstruction_NonGeneralPresets_AreAllDistinct()
    {
        var sut = new PromptTemplateProvider();

        var instructions = new[]
        {
            sut.GetPresetInstruction(PromptPreset.Feature),
            sut.GetPresetInstruction(PromptPreset.Bugfix),
            sut.GetPresetInstruction(PromptPreset.Refactor),
            sut.GetPresetInstruction(PromptPreset.Explain),
        };

        Assert.Equal(instructions.Length, instructions.Distinct().Count());
    }

    [Fact]
    public void GetDepthInstruction_Standard_ReturnsEmpty()
    {
        var sut = new PromptTemplateProvider();

        Assert.Equal(string.Empty, sut.GetDepthInstruction(TutorialDepth.Standard));
    }

    [Theory]
    [InlineData(TutorialDepth.Brief)]
    [InlineData(TutorialDepth.Detailed)]
    public void GetDepthInstruction_NonStandardDepth_ReturnsNonEmptyText(TutorialDepth depth)
    {
        var sut = new PromptTemplateProvider();

        var instruction = sut.GetDepthInstruction(depth);

        Assert.False(string.IsNullOrWhiteSpace(instruction));
    }

    [Fact]
    public void GetDepthInstruction_BriefAndDetailed_AreDistinct()
    {
        var sut = new PromptTemplateProvider();

        var brief = sut.GetDepthInstruction(TutorialDepth.Brief);
        var detailed = sut.GetDepthInstruction(TutorialDepth.Detailed);

        Assert.NotEqual(brief, detailed);
    }
}
