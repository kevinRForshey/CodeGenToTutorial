using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;
using Paarde.Core.Models;
using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Tests.ViewModels;

public class TutorialStepViewModelTests
{
    private readonly FakeFileChangeApplyService _fileChangeApplyService = new();
    private readonly FakeLlmRunner _llmRunner = new();

    private TutorialStepViewModel CreateSut(string? resumeSessionId = "session-1") =>
        new(
            new ProposedHunk("Step 1", "Explanation", "snippet", null),
            new ProposedFileChangeViewModel("a.cs", "proposed"),
            "/tmp",
            resumeSessionId,
            _fileChangeApplyService,
            _llmRunner,
            stepNumber: 1,
            totalSteps: 1);

    [Fact]
    public void StepLabel_ReflectsTheConstructedStepNumber()
    {
        var sut = new TutorialStepViewModel(
            new ProposedHunk("Step 2", "Explanation", "snippet", null),
            new ProposedFileChangeViewModel("a.cs", "proposed"),
            "/tmp",
            "session-1",
            _fileChangeApplyService,
            _llmRunner,
            stepNumber: 2,
            totalSteps: 4);

        Assert.Equal(2, sut.StepNumber);
        Assert.Equal(4, sut.TotalSteps);
        Assert.Equal("Step 2:", sut.StepLabel);
    }

    [Fact]
    public async Task ExplainFurtherAsync_Success_SetsFurtherExplanationFromPlainProseOutput()
    {
        _llmRunner.ResultToReturn = new LlmRunResult { Success = true, Output = "Here's a deeper explanation." };
        var sut = CreateSut();

        await sut.ExplainFurtherCommand.ExecuteAsync(null);

        Assert.Equal("Here's a deeper explanation.", sut.FurtherExplanation);
    }

    [Fact]
    public async Task ExplainFurtherAsync_Success_ResumesTheTrackedSessionId()
    {
        _llmRunner.ResultToReturn = new LlmRunResult { Success = true, Output = "deeper" };
        var sut = CreateSut(resumeSessionId: "session-42");

        await sut.ExplainFurtherCommand.ExecuteAsync(null);

        Assert.Equal("session-42", _llmRunner.LastResumeSessionId);
    }

    [Fact]
    public async Task ExplainFurtherAsync_OutputIsJsonWrapped_UnwrapsTheProseAnswer()
    {
        _llmRunner.ResultToReturn = new LlmRunResult
        {
            Success = true,
            Output = """{"responseType": "answer", "answer": "The unwrapped answer."}""",
        };
        var sut = CreateSut();

        await sut.ExplainFurtherCommand.ExecuteAsync(null);

        Assert.Equal("The unwrapped answer.", sut.FurtherExplanation);
    }

    [Fact]
    public async Task ExplainFurtherAsync_Failure_SetsFurtherExplanationToAnErrorMessage()
    {
        _llmRunner.ResultToReturn = new LlmRunResult { Success = false, ErrorMessage = "boom" };
        var sut = CreateSut();

        await sut.ExplainFurtherCommand.ExecuteAsync(null);

        Assert.Contains("boom", sut.FurtherExplanation);
    }

    [Fact]
    public void CanExplainFurther_WhileExplaining_CannotBeExecuted()
    {
        var sut = CreateSut();

        sut.IsExplaining = true;

        Assert.False(sut.ExplainFurtherCommand.CanExecute(null));
    }
}
