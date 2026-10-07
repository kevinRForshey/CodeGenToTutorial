using CodeGenToTutorial.Avalonia.Strings;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.Avalonia.ViewModels;

// Wraps a single ProposedHunk with the ability to apply its file's proposed change and report the
// outcome. All steps for the same file share one "applied" state, since applying any of them writes
// the same complete file content.
public partial class TutorialStepViewModel : ObservableObject
{
    private readonly ProposedFileChangeViewModel _file;
    private readonly string _workingDirectoryPath;
    private readonly IFileChangeApplyService _fileChangeApplyService;
    private readonly Action _onApplied;

    public string Title { get; }

    public string Explanation { get; }

    public string Code { get; }

    public string FilePath { get; }

    [ObservableProperty]
    private bool isApplied;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public TutorialStepViewModel(ProposedHunk hunk, ProposedFileChangeViewModel file, string workingDirectoryPath, IFileChangeApplyService fileChangeApplyService, Action onApplied)
    {
        Title = hunk.Title;
        Explanation = hunk.Explanation;
        Code = hunk.Snippet;
        FilePath = file.FilePath;
        _file = file;
        _workingDirectoryPath = workingDirectoryPath;
        _fileChangeApplyService = fileChangeApplyService;
        _onApplied = onApplied;
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        try
        {
            _fileChangeApplyService.Apply(_file, _workingDirectoryPath);
            StatusMessage = string.Format("Tutorial_ChangeApplied".GetLocalized(), _file.FilePath);
            _onApplied();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage = string.Format("Tutorial_ApplyFailed".GetLocalized(), ex.Message);
        }
    }

    private bool CanApply() => !IsApplied && !string.IsNullOrWhiteSpace(_workingDirectoryPath);

    partial void OnIsAppliedChanged(bool value) => ApplyCommand.NotifyCanExecuteChanged();
}
