using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Models;
using Paarde.Avalonia.Strings;
using Paarde.Core.Contracts.Services;
using Paarde.Core.Helpers;
using Paarde.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Paarde.Avalonia.ViewModels;

// Wraps a single ProposedHunk with the ability to apply its file's proposed change and report the
// outcome. Applying any step writes the same complete file (one write, selection-aware content via
// ProposedFileChangeViewModel.BuildSelectedContent - see FileChangeApplyService), but "applied"
// itself is per-step: FileChangeApplyService.MarkHunksAppliedFromSelection sets Hunk.IsApplied to
// Hunk.IsSelected right after that write, so a deselected step never reports itself as applied just
// because a sibling step's button was clicked (T3.3.2 - replaces the old MarkAllStepsApplied()
// workaround that marked every step on the file applied unconditionally).
public partial class TutorialStepViewModel : ObservableObject
{
    private readonly ProposedFileChangeViewModel _file;
    private readonly string _workingDirectoryPath;
    private readonly string? _resumeSessionId;
    private readonly IFileChangeApplyService _fileChangeApplyService;
    private readonly ILlmRunner _llmRunner;
    private readonly INavigationService _navigationService;
    private readonly ILogger<TutorialStepViewModel> _logger;

    public string Title { get; }

    public string Explanation { get; }

    // Exposed so the view can bind its selection/applied state directly to Hunk's properties rather
    // than this class forwarding/duplicating them.
    public ProposedHunk Hunk { get; }

    // T8.4.3: this step's 1-based position among its file's steps, and how many there are in total -
    // set by TutorialDetailControl.RebuildSteps from the hunk's index in ProposedFileChangeViewModel.
    // Hunks, which is also the order the steps list (and step-navigation) displays them in.
    public int StepNumber { get; }

    public int TotalSteps { get; }

    public string StepLabel => $"Step {StepNumber}:";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // T7.3.2: the deeper explanation from ExplainFurtherCommand, empty until that's been clicked.
    [ObservableProperty]
    private string furtherExplanation = string.Empty;

    [ObservableProperty]
    private bool isExplaining;

    public TutorialStepViewModel(ProposedHunk hunk, ProposedFileChangeViewModel file, string workingDirectoryPath, string? resumeSessionId, IFileChangeApplyService fileChangeApplyService, ILlmRunner llmRunner, INavigationService navigationService, int stepNumber, int totalSteps, ILogger<TutorialStepViewModel>? logger = null)
    {
        Title = hunk.Title;
        Explanation = hunk.Explanation;
        Hunk = hunk;
        StepNumber = stepNumber;
        TotalSteps = totalSteps;
        _file = file;
        _workingDirectoryPath = workingDirectoryPath;
        _resumeSessionId = resumeSessionId;
        _fileChangeApplyService = fileChangeApplyService;
        _llmRunner = llmRunner;
        _navigationService = navigationService;
        _logger = logger ?? NullLogger<TutorialStepViewModel>.Instance;

        // Hunk.IsApplied/MatchedDiffHunks live on a different ObservableObject than this view
        // model's own [ObservableProperty]s, so CanApply/CanViewInDiff need an explicit nudge
        // whenever either changes.
        Hunk.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProposedHunk.IsApplied))
            {
                ApplyCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(ProposedHunk.MatchedDiffHunks))
            {
                ViewInDiffCommand.NotifyCanExecuteChanged();
            }
        };
    }

    partial void OnIsExplainingChanged(bool value) => ExplainFurtherCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        try
        {
            _fileChangeApplyService.Apply(_file, _workingDirectoryPath);
            StatusMessage = string.Format("Tutorial_ChangeApplied".GetLocalized(), _file.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to apply {FilePath} in {WorkingDirectory}.", _file.FilePath, _workingDirectoryPath);
            StatusMessage = string.Format("Tutorial_ApplyFailed".GetLocalized(), ex.Message);
        }
    }

    private bool CanApply() => !Hunk.IsApplied && !string.IsNullOrWhiteSpace(_workingDirectoryPath);

    // T8.4.5: jumps to this step's change on the Diffs page. Disabled when the step's Location
    // didn't resolve to any DiffHunk (e.g. the snippet turned out identical to what was already
    // there) - there's nothing to scroll to in that case.
    [RelayCommand(CanExecute = nameof(CanViewInDiff))]
    private void ViewInDiff() =>
        _navigationService.NavigateTo(typeof(DiffsViewModel).FullName!, new DiffJumpRequest(_file, Hunk.MatchedDiffHunks[0], Hunk));

    private bool CanViewInDiff() => Hunk.MatchedDiffHunks.Count > 0;

    // T7.3.2: a scoped follow-up asking Claude to go deeper on just this one step, resuming the
    // conversation that produced it (T7.3.1) rather than starting fresh - so it already has the
    // whole file/tutorial as context and only needs to be told which step to elaborate on.
    [RelayCommand(CanExecute = nameof(CanExplainFurther))]
    private async Task ExplainFurtherAsync()
    {
        IsExplaining = true;

        try
        {
            var prompt =
                $"Explain this tutorial step in more depth than the explanation below already does. " +
                $"Respond in plain prose only - no JSON, no markdown fences.\n\n" +
                $"Step: {Title}\n\nExplanation so far: {Explanation}\n\nRelevant code:\n{Hunk.Snippet}";

            var result = await _llmRunner.RunPromptAsync(prompt, _workingDirectoryPath, resumeSessionId: _resumeSessionId);

            FurtherExplanation = result.Success
                ? ExtractProseAnswer(result.Output)
                : string.Format("Tutorial_ExplainFurtherFailed".GetLocalized(), result.ErrorMessage);
        }
        finally
        {
            IsExplaining = false;
        }
    }

    private bool CanExplainFurther() => !IsExplaining;

    // Asked explicitly for plain prose above, but the resumed session's first turn insisted on the
    // JSON contract, so a model that answers in JSON anyway is handled rather than shown raw - .Tutorial
    // doubles as the prose text for both an "answer" and a "development" response (see ClaudeParsedResponse).
    private static string ExtractProseAnswer(string output) =>
        ClaudeJsonResponseParser.TryParse(output, out var response, out _) ? response!.Tutorial : output.Trim();
}
