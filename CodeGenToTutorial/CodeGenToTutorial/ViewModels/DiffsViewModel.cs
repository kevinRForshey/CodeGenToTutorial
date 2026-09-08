using System.Collections.ObjectModel;

using CodeGenToTutorial.Contracts.Services;
using CodeGenToTutorial.Contracts.ViewModels;
using CodeGenToTutorial.Helpers;
using CodeGenToTutorial.Models;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeGenToTutorial.ViewModels;

public partial class DiffsViewModel : ObservableRecipient, INavigationAware
{
    private readonly IPromptResultStore _promptResultStore;

    [ObservableProperty]
    private ProposedFileChange? selected;

    public ObservableCollection<ProposedFileChange> Files { get; } = new();

    public DiffsViewModel(IPromptResultStore promptResultStore)
    {
        _promptResultStore = promptResultStore;
    }

    public void OnNavigatedTo(object parameter)
    {
        Files.Clear();

        var workingDirectory = _promptResultStore.WorkingDirectoryPath;

        foreach (var file in _promptResultStore.Files)
        {
            var originalContent = ReadOriginalContent(workingDirectory, file.FilePath);

            Files.Add(new ProposedFileChange
            {
                FilePath = file.FilePath,
                Content = file.Content,
                DiffLines = DiffBuilder.BuildLineDiff(originalContent, file.Content),
                TutorialSteps = file.TutorialSteps,
            });
        }

        Selected = null;
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
