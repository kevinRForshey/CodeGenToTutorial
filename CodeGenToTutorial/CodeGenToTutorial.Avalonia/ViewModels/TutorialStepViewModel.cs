using CodeGenToTutorial.Avalonia.Strings;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.Avalonia.ViewModels;

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
    private readonly IFileChangeApplyService _fileChangeApplyService;

    public string Title { get; }

    public string Explanation { get; }

    // Exposed so the view can bind its selection/applied state directly to Hunk's properties rather
    // than this class forwarding/duplicating them.
    public ProposedHunk Hunk { get; }

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public TutorialStepViewModel(ProposedHunk hunk, ProposedFileChangeViewModel file, string workingDirectoryPath, IFileChangeApplyService fileChangeApplyService)
    {
        Title = hunk.Title;
        Explanation = hunk.Explanation;
        Hunk = hunk;
        _file = file;
        _workingDirectoryPath = workingDirectoryPath;
        _fileChangeApplyService = fileChangeApplyService;

        // Hunk.IsApplied lives on a different ObservableObject than this view model's own
        // [ObservableProperty]s, so CanApply needs an explicit nudge whenever it changes.
        Hunk.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProposedHunk.IsApplied))
            {
                ApplyCommand.NotifyCanExecuteChanged();
            }
        };
    }

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
            StatusMessage = string.Format("Tutorial_ApplyFailed".GetLocalized(), ex.Message);
        }
    }

    private bool CanApply() => !Hunk.IsApplied && !string.IsNullOrWhiteSpace(_workingDirectoryPath);
}
