using CodeGenToTutorial.Core.Models;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeGenToTutorial.Core.ViewModels;

// Mutable, observable replacement for the old init-only ProposedFileChange record. Tracks the file's
// three content states - OriginalContent (on disk today), ProposedContent (the LLM's output), and
// EditedContent (what the user has changed it to, starting equal to ProposedContent) - so an in-app
// editor (Epic 2.2) can diverge from the model's output without losing track of either source. Lives
// in Core rather than the Avalonia project because PromptResultStore (also Core) holds the shared
// graph of these that both the Tutorial and Diffs pages bind to.
public partial class ProposedFileChangeViewModel : ObservableObject
{
    public string FilePath { get; }

    public string OriginalContent { get; }

    public string ProposedContent { get; }

    public IReadOnlyList<DiffLine> DiffLines { get; init; } = Array.Empty<DiffLine>();

    public IReadOnlyList<ProposedHunk> Hunks { get; init; } = Array.Empty<ProposedHunk>();

    [ObservableProperty]
    private string editedContent;

    [ObservableProperty]
    private bool isDirty;

    public ProposedFileChangeViewModel(string filePath, string proposedContent, string originalContent = "")
    {
        FilePath = filePath;
        ProposedContent = proposedContent;
        OriginalContent = originalContent;
        editedContent = proposedContent;
    }

    partial void OnEditedContentChanged(string value) => IsDirty = value != ProposedContent;
}
