using System.Collections.ObjectModel;

using CodeGenToTutorial.Avalonia.Contracts.ViewModels;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeGenToTutorial.Avalonia.ViewModels;

public partial class DiffsViewModel : ObservableRecipient, INavigationAware
{
    private readonly IPromptResultStore _promptResultStore;

    [ObservableProperty]
    private ProposedFileChangeViewModel? selected;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

    public DiffsViewModel(IPromptResultStore promptResultStore)
    {
        _promptResultStore = promptResultStore;
    }

    public void OnNavigatedTo(object parameter)
    {
        Files.Clear();

        var workingDirectory = _promptResultStore.WorkingDirectoryPath;

        // Reuse the store's own instances - the same ones the Tutorial page edits - rather than
        // building copies, so DiffLines (recomputed inside ProposedFileChangeViewModel) reflects the
        // user's live edits instead of a snapshot of the raw proposal taken at navigation time.
        foreach (var file in _promptResultStore.Files)
        {
            file.OriginalContent = ReadOriginalContent(workingDirectory, file.FilePath);
            Files.Add(file);
        }

        // The WinUI version only auto-selects the first file once the master/detail view reports it has
        // room to show both panes side by side; the hand-built two-pane layout here always shows both,
        // so it's safe to select eagerly instead of waiting for a ViewStateChanged-equivalent signal.
        EnsureItemSelected();
    }

    // Original file may not exist yet (new file) or the working directory may not be set; either case
    // is treated as an empty original, so the whole proposed content shows up as added lines.
    private static string ReadOriginalContent(string workingDirectory, string relativeFilePath)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || string.IsNullOrWhiteSpace(relativeFilePath))
        {
            return string.Empty;
        }

        try
        {
            var fullPath = Path.Combine(workingDirectory, relativeFilePath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public void OnNavigatedFrom()
    {
    }

    public void EnsureItemSelected()
    {
        Selected ??= Files.FirstOrDefault();
    }
}
