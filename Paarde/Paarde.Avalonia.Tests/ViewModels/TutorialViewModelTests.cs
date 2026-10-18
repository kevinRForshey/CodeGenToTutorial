using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;
using Paarde.Core.Models;
using Paarde.Core.Services;
using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Tests.ViewModels;

public class TutorialViewModelTests
{
    private readonly FakePromptResultStore _promptResultStore = new();
    private readonly FakeApplyOrchestrationService _applyOrchestrationService = new();
    private readonly FakeApplyHistoryLog _applyHistoryLog = new();
    private readonly FakeDialogService _dialogService = new();
    private readonly FakeLlmRunner _llmRunner = new();
    private readonly PromptTemplateProvider _promptTemplateProvider = new();

    private TutorialViewModel CreateSut() => new(_promptResultStore, _applyOrchestrationService, _applyHistoryLog, _dialogService, _llmRunner, _promptTemplateProvider);

    [Fact]
    public void HasUnsavedChanges_NoFiles_IsFalse()
    {
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        Assert.False(sut.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_NoFileIsDirty_IsFalse()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        Assert.False(sut.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_OneFileIsDirty_IsTrue()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        sut.Files[0].EditedContent = "edited";

        Assert.True(sut.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_DirtyFileRevertedBackToProposed_IsFalseAgain()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.Files[0].EditedContent = "edited";

        sut.Files[0].RevertToProposedCommand.Execute(null);

        Assert.False(sut.HasUnsavedChanges);
    }

    [Fact]
    public void HasNoFiles_NoPromptRunYet_IsTrue()
    {
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        Assert.True(sut.HasNoFiles);
        Assert.False(sut.HasFilesButNoneSelected);
    }

    [Fact]
    public void HasFilesButNoneSelected_FilesPresent_IsFalseOnceOnNavigatedToAutoSelectsTheFirstOne()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        // OnNavigatedTo already calls EnsureItemSelected, so with files present there's always a
        // selection by the time the page is shown - HasFilesButNoneSelected only guards the moment
        // between the two, or a Selected that's been explicitly cleared.
        Assert.False(sut.HasNoFiles);
        Assert.False(sut.HasFilesButNoneSelected);
    }

    [Fact]
    public void HasFilesButNoneSelected_FilesPresentButSelectionCleared_IsTrue()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        sut.Selected = null;

        Assert.True(sut.HasFilesButNoneSelected);
    }

    [Fact]
    public async Task ApplySelectedAsync_NothingSelected_SkipsConfirmationAndReportsNoneSelected()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(0, 0);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(0, _dialogService.ConfirmCallCount);
        Assert.Equal(0, _applyOrchestrationService.ApplySelectedCallCount);
        Assert.False(string.IsNullOrEmpty(sut.ApplyStatusMessage));
    }

    [Fact]
    public async Task ApplySelectedAsync_UserCancelsConfirmation_DoesNotApply()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1);
        _dialogService.ConfirmResultToReturn = false;
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogService.ConfirmCallCount);
        Assert.Equal(0, _applyOrchestrationService.ApplySelectedCallCount);
    }

    [Fact]
    public async Task ApplySelectedAsync_UserConfirms_AppliesAndReportsResultViaInfoDialog()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(2, 3);
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.ApplySelectedResultToReturn = new ApplyBatchResult(
            [new FileApplyResult("a.txt", FileApplyOutcome.Applied)]);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, _applyOrchestrationService.ApplySelectedCallCount);
        Assert.Equal(1, _dialogService.InfoCallCount);
        Assert.Contains("1", sut.ApplyStatusMessage);
    }

    [Fact]
    public async Task ApplySelectedAsync_SomeFilesConflictOrFail_MessageMentionsEachAffectedFile()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(2, 2);
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.ApplySelectedResultToReturn = new ApplyBatchResult(
        [
            new FileApplyResult("conflicted.txt", FileApplyOutcome.Conflict),
            new FileApplyResult("failed.txt", FileApplyOutcome.Failed, "disk full"),
        ]);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Contains("conflicted.txt", sut.ApplyStatusMessage);
        Assert.Contains("failed.txt", sut.ApplyStatusMessage);
    }

    [Fact]
    public async Task ApplySelectedAsync_NoProtectedFiles_OnlyShowsOneConfirmationDialog()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1) { ProtectedPathCount = 0 };
        _dialogService.ConfirmResultToReturn = true;
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogService.ConfirmCallCount);
        Assert.True(_applyOrchestrationService.LastAllowProtectedPaths);
    }

    [Fact]
    public async Task ApplySelectedAsync_ProtectedFilesAndUserConfirmsBoth_AppliesWithProtectedPathsAllowed()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1) { ProtectedPathCount = 1, ProtectedPathFiles = ["bin/output.txt"] };
        _dialogService.ConfirmResultQueue.Enqueue(true);
        _dialogService.ConfirmResultQueue.Enqueue(true);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogService.ConfirmCallCount);
        Assert.True(_applyOrchestrationService.LastAllowProtectedPaths);
        Assert.Contains("bin/output.txt", _dialogService.ConfirmMessages[1]);
    }

    [Fact]
    public async Task ApplySelectedAsync_ProtectedFilesAndUserDeclinesProtectedConfirm_StillAppliesWithoutAllowingProtectedPaths()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1) { ProtectedPathCount = 1, ProtectedPathFiles = ["bin/output.txt"] };
        _dialogService.ConfirmResultQueue.Enqueue(true);
        _dialogService.ConfirmResultQueue.Enqueue(false);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, _applyOrchestrationService.ApplySelectedCallCount);
        Assert.False(_applyOrchestrationService.LastAllowProtectedPaths);
    }

    [Fact]
    public async Task ApplySelectedAsync_UserDeclinesGeneralConfirm_NeverAsksAboutProtectedPaths()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1) { ProtectedPathCount = 1, ProtectedPathFiles = ["bin/output.txt"] };
        _dialogService.ConfirmResultQueue.Enqueue(false);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogService.ConfirmCallCount);
        Assert.Equal(0, _applyOrchestrationService.ApplySelectedCallCount);
    }

    [Fact]
    public async Task ApplySelectedAsync_NoGitDirtyFiles_OnlyShowsOneConfirmationDialog()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1) { IsGitRepository = true, GitDirtyFileCount = 0 };
        _dialogService.ConfirmResultToReturn = true;
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogService.ConfirmCallCount);
        Assert.Equal(1, _applyOrchestrationService.ApplySelectedCallCount);
    }

    [Fact]
    public async Task ApplySelectedAsync_GitDirtyFilesAndUserConfirmsBoth_Applies()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1) { IsGitRepository = true, GitDirtyFileCount = 1, GitDirtyFiles = ["a.txt"] };
        _dialogService.ConfirmResultQueue.Enqueue(true);
        _dialogService.ConfirmResultQueue.Enqueue(true);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogService.ConfirmCallCount);
        Assert.Equal(1, _applyOrchestrationService.ApplySelectedCallCount);
        Assert.Contains("a.txt", _dialogService.ConfirmMessages[1]);
    }

    [Fact]
    public async Task ApplySelectedAsync_GitDirtyFilesAndUserDeclinesDirtyConfirm_CancelsTheWholeApply()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1) { IsGitRepository = true, GitDirtyFileCount = 1, GitDirtyFiles = ["a.txt"] };
        _dialogService.ConfirmResultQueue.Enqueue(true);
        _dialogService.ConfirmResultQueue.Enqueue(false);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogService.ConfirmCallCount);
        Assert.Equal(0, _applyOrchestrationService.ApplySelectedCallCount);
    }

    [Fact]
    public async Task ApplySelectedAsync_UserDeclinesGeneralConfirm_NeverAsksAboutGitDirtyFiles()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1) { IsGitRepository = true, GitDirtyFileCount = 1, GitDirtyFiles = ["a.txt"] };
        _dialogService.ConfirmResultQueue.Enqueue(false);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogService.ConfirmCallCount);
        Assert.Equal(0, _applyOrchestrationService.ApplySelectedCallCount);
    }

    [Fact]
    public async Task ApplySelectedAsync_SomeFilesProtectedPathBlocked_MessageMentionsEachBlockedFile()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1);
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.ApplySelectedResultToReturn = new ApplyBatchResult(
            [new FileApplyResult("bin/output.txt", FileApplyOutcome.ProtectedPathBlocked)]);
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Contains("bin/output.txt", sut.ApplyStatusMessage);
    }

    [Fact]
    public async Task UndoLastApplyAsync_UserCancelsConfirmation_DoesNotUndo()
    {
        _dialogService.ConfirmResultToReturn = false;
        var sut = CreateSut();

        await sut.UndoLastApplyCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogService.ConfirmCallCount);
        Assert.Equal(0, _applyOrchestrationService.TryUndoLastApplyCallCount);
    }

    [Fact]
    public async Task UndoLastApplyAsync_NothingToUndo_ReportsItWithoutShowingResultDialog()
    {
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.TryUndoLastApplyResultToReturn = false;
        var sut = CreateSut();

        await sut.UndoLastApplyCommand.ExecuteAsync(null);

        Assert.Equal(0, _dialogService.InfoCallCount);
        Assert.False(string.IsNullOrEmpty(sut.ApplyStatusMessage));
    }

    [Fact]
    public async Task UndoLastApplyAsync_UserConfirms_UndoesAndReportsResultViaInfoDialog()
    {
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.TryUndoLastApplyResultToReturn = true;
        _applyOrchestrationService.UndoLastApplyRevertResultToReturn = new RevertResult([new RevertFileResult("a.txt", true)]);
        var sut = CreateSut();

        await sut.UndoLastApplyCommand.ExecuteAsync(null);

        Assert.Equal(1, _applyOrchestrationService.TryUndoLastApplyCallCount);
        Assert.Equal(1, _dialogService.InfoCallCount);
        Assert.Contains("1", sut.ApplyStatusMessage);
    }

    [Fact]
    public async Task UndoLastApplyAsync_RestoredFileIsInFilesCollection_RefreshesItsOriginalContent()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "stale baseline")], Path.GetTempPath());
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.TryUndoLastApplyResultToReturn = true;
        _applyOrchestrationService.UndoLastApplyRevertResultToReturn = new RevertResult([new RevertFileResult("a.txt", true)]);
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        await sut.UndoLastApplyCommand.ExecuteAsync(null);

        Assert.NotEqual("stale baseline", sut.Files[0].OriginalContent);
    }

    [Fact]
    public async Task UndoLastApplyAsync_SomeFilesFailedToRestore_MessageMentionsEachFailedFile()
    {
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.TryUndoLastApplyResultToReturn = true;
        _applyOrchestrationService.UndoLastApplyRevertResultToReturn = new RevertResult(
            [new RevertFileResult("failed.txt", false, "disk full")]);
        var sut = CreateSut();

        await sut.UndoLastApplyCommand.ExecuteAsync(null);

        Assert.Contains("failed.txt", sut.ApplyStatusMessage);
    }

    [Fact]
    public async Task RevertAllAppliedAsync_UserCancelsConfirmation_DoesNotRevert()
    {
        _dialogService.ConfirmResultToReturn = false;
        var sut = CreateSut();

        await sut.RevertAllAppliedCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogService.ConfirmCallCount);
        Assert.Equal(0, _applyOrchestrationService.RevertAllAppliedCallCount);
    }

    [Fact]
    public async Task RevertAllAppliedAsync_NothingToRevert_ReportsItWithoutShowingResultDialog()
    {
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.RevertAllAppliedResultToReturn = new RevertResult(Array.Empty<RevertFileResult>());
        var sut = CreateSut();

        await sut.RevertAllAppliedCommand.ExecuteAsync(null);

        Assert.Equal(0, _dialogService.InfoCallCount);
        Assert.False(string.IsNullOrEmpty(sut.ApplyStatusMessage));
    }

    [Fact]
    public async Task RevertAllAppliedAsync_UserConfirms_RevertsAndReportsResultViaInfoDialog()
    {
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.RevertAllAppliedResultToReturn = new RevertResult(
            [new RevertFileResult("a.txt", true), new RevertFileResult("b.txt", true)]);
        var sut = CreateSut();

        await sut.RevertAllAppliedCommand.ExecuteAsync(null);

        Assert.Equal(1, _applyOrchestrationService.RevertAllAppliedCallCount);
        Assert.Equal(1, _dialogService.InfoCallCount);
        Assert.Contains("2", sut.ApplyStatusMessage);
    }

    [Fact]
    public async Task RevertAllAppliedAsync_SomeFilesFailedToRevert_MessageMentionsEachFailedFile()
    {
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.RevertAllAppliedResultToReturn = new RevertResult(
            [new RevertFileResult("failed.txt", false, "disk full")]);
        var sut = CreateSut();

        await sut.RevertAllAppliedCommand.ExecuteAsync(null);

        Assert.Contains("failed.txt", sut.ApplyStatusMessage);
    }

    [Fact]
    public void HasApplyHistory_NoHistoryLogged_IsFalse()
    {
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        Assert.False(sut.HasApplyHistory);
        Assert.Empty(sut.ApplyHistoryEntries);
    }

    [Fact]
    public void OnNavigatedTo_HistoryAlreadyLogged_PopulatesApplyHistoryEntries()
    {
        _applyHistoryLog.Record(ApplyHistoryEntryKind.Applied, new[] { "a.txt" });
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.True(sut.HasApplyHistory);
        Assert.Single(sut.ApplyHistoryEntries);
    }

    [Fact]
    public async Task ApplySelectedAsync_UserConfirms_AddsAnEntryToApplyHistoryEntries()
    {
        _applyOrchestrationService.PreviewToReturn = new ApplyPreview(1, 1);
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.ApplySelectedResultToReturn = new ApplyBatchResult(
            [new FileApplyResult("a.txt", FileApplyOutcome.Applied)]);
        _applyHistoryLog.Record(ApplyHistoryEntryKind.Applied, new[] { "a.txt" });
        var sut = CreateSut();

        await sut.ApplySelectedCommand.ExecuteAsync(null);

        Assert.True(sut.HasApplyHistory);
        Assert.Single(sut.ApplyHistoryEntries);
    }

    [Fact]
    public async Task UndoLastApplyAsync_UserConfirms_RefreshesApplyHistoryEntriesNewestFirst()
    {
        // The real IApplyHistoryLog entry for this undo is recorded by ApplyOrchestrationService
        // itself (not the fake standing in for it here), so seed the log the way it would look right
        // after a real TryUndoLastApply call, then verify the ViewModel's post-action refresh picks it up.
        _applyHistoryLog.Record(ApplyHistoryEntryKind.Applied, new[] { "a.txt" });
        _applyHistoryLog.Record(ApplyHistoryEntryKind.UndoLastApply, new[] { "a.txt" });
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.TryUndoLastApplyResultToReturn = true;
        _applyOrchestrationService.UndoLastApplyRevertResultToReturn = new RevertResult([new RevertFileResult("a.txt", true)]);
        var sut = CreateSut();

        await sut.UndoLastApplyCommand.ExecuteAsync(null);

        Assert.Equal(2, sut.ApplyHistoryEntries.Count);
        Assert.Contains("Undid last apply", sut.ApplyHistoryEntries[0]);
    }

    [Fact]
    public async Task RevertAllAppliedAsync_UserConfirms_RefreshesApplyHistoryEntries()
    {
        // Same reasoning as the undo test above: seed the log entry the real orchestration service
        // would have recorded, then verify the ViewModel's post-action refresh displays it.
        _applyHistoryLog.Record(ApplyHistoryEntryKind.RevertAllApplied, new[] { "a.txt" });
        _dialogService.ConfirmResultToReturn = true;
        _applyOrchestrationService.RevertAllAppliedResultToReturn = new RevertResult(
            [new RevertFileResult("a.txt", true)]);
        var sut = CreateSut();

        await sut.RevertAllAppliedCommand.ExecuteAsync(null);

        Assert.True(sut.HasApplyHistory);
        Assert.Contains("Reverted all applied changes", sut.ApplyHistoryEntries[0]);
    }

    [Fact]
    public void CanRefineSelectedFile_NoFileSelected_CannotBeExecuted()
    {
        var sut = CreateSut();
        sut.RefineInstruction = "make it better";

        Assert.False(sut.RefineSelectedFileCommand.CanExecute(null));
    }

    [Fact]
    public void CanRefineSelectedFile_NoInstructionText_CannotBeExecuted()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        Assert.False(sut.RefineSelectedFileCommand.CanExecute(null));
    }

    [Fact]
    public void CanRefineSelectedFile_FileSelectedAndInstructionGiven_CanBeExecuted()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.RefineInstruction = "make it better";

        Assert.True(sut.RefineSelectedFileCommand.CanExecute(null));
    }

    [Fact]
    public async Task RefineSelectedFileAsync_Success_ReplacesTheFileInBothFilesAndThePromptResultStore()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp", "session-1");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.RefineInstruction = "add a comment";
        _llmRunner.ResultToReturn = new LlmRunResult
        {
            Success = true,
            Output = """{"responseType": "development", "tutorial": "Refined.", "files": [{"path": "a.txt", "content": "refined content", "steps": []}]}""",
        };

        await sut.RefineSelectedFileCommand.ExecuteAsync(null);

        Assert.Equal("refined content", sut.Files[0].ProposedContent);
        Assert.Equal("refined content", _promptResultStore.Files[0].ProposedContent);
        Assert.Same(sut.Files[0], sut.Selected);
    }

    [Fact]
    public async Task RefineSelectedFileAsync_Success_ResumesTheTrackedSessionIdAndClearsTheInstruction()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp", "session-1");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.RefineInstruction = "add a comment";
        _llmRunner.ResultToReturn = new LlmRunResult
        {
            Success = true,
            Output = """{"responseType": "development", "tutorial": "Refined.", "files": [{"path": "a.txt", "content": "refined content", "steps": []}]}""",
        };

        await sut.RefineSelectedFileCommand.ExecuteAsync(null);

        Assert.Equal("session-1", _llmRunner.LastResumeSessionId);
        Assert.Equal(string.Empty, sut.RefineInstruction);
    }

    [Fact]
    public async Task RefineSelectedFileAsync_RunFails_ReportsTheErrorWithoutReplacingTheFile()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.RefineInstruction = "add a comment";
        _llmRunner.ResultToReturn = new LlmRunResult { Success = false, ErrorMessage = "boom" };
        var originalFile = sut.Files[0];

        await sut.RefineSelectedFileCommand.ExecuteAsync(null);

        Assert.Same(originalFile, sut.Files[0]);
        Assert.Contains("a.txt", sut.RefineStatusMessage);
        Assert.Contains("boom", sut.RefineStatusMessage);
    }

    [Fact]
    public async Task RefineSelectedFileAsync_ResponseHasNoMatchingFile_ReportsItWithoutReplacingTheFile()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.RefineInstruction = "add a comment";
        _llmRunner.ResultToReturn = new LlmRunResult
        {
            Success = true,
            Output = """{"responseType": "development", "tutorial": "Refined.", "files": []}""",
        };
        var originalFile = sut.Files[0];

        await sut.RefineSelectedFileCommand.ExecuteAsync(null);

        Assert.Same(originalFile, sut.Files[0]);
        Assert.Contains("a.txt", sut.RefineStatusMessage);
    }
}
