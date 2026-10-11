using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.Core.ViewModels;

// Mutable, observable replacement for the old init-only ProposedFileChange record. Tracks the file's
// three content states - OriginalContent (on disk today), ProposedContent (the LLM's output), and
// EditedContent (what the user has changed it to, starting equal to ProposedContent) - so an in-app
// editor (Epic 2.2) can diverge from the model's output without losing track of either source. Lives
// in Core rather than the Avalonia project because PromptResultStore (also Core) holds the shared
// graph of these that both the Tutorial and Diffs pages bind to - the same instance flows into both,
// so DiffLines (computed, not settable) recomputes itself whenever OriginalContent or EditedContent
// change, keeping the Diffs page in sync with edits made on the Tutorial page without either page
// having to ask the other to refresh.
public partial class ProposedFileChangeViewModel : ObservableObject
{
    public string FilePath { get; }

    public string ProposedContent { get; }

    private IReadOnlyList<ProposedHunk> hunks = Array.Empty<ProposedHunk>();

    // init rather than a plain [ObservableProperty] field, since the set of steps a response
    // parses out doesn't change after construction - but it still needs to run the same
    // matching logic as OnDiffHunksChanged below, because it's assigned via an object initializer
    // (ClaudeResponseParser.Parse), which runs after the constructor - and therefore after
    // DiffHunks already has its initial value.
    public IReadOnlyList<ProposedHunk> Hunks
    {
        get => hunks;
        init
        {
            hunks = value;
            UpdateMatchedDiffHunks();
        }
    }

    [ObservableProperty]
    private string originalContent;

    [ObservableProperty]
    private string editedContent;

    [ObservableProperty]
    private bool isDirty;

    [ObservableProperty]
    private IReadOnlyList<DiffLine> diffLines = Array.Empty<DiffLine>();

    // Unified-diff-style grouping of DiffLines (see DiffBuilder.GroupIntoHunks), kept in sync with
    // DiffLines rather than recomputed on demand so Hunks' MatchedDiffHunks (below) can be updated
    // reactively via OnDiffHunksChanged instead of every consumer having to remember to re-match.
    [ObservableProperty]
    private IReadOnlyList<DiffHunk> diffHunks = Array.Empty<DiffHunk>();

    public ProposedFileChangeViewModel(string filePath, string proposedContent, string originalContent = "")
    {
        FilePath = filePath;
        ProposedContent = proposedContent;
        this.originalContent = originalContent;
        editedContent = proposedContent;
        diffLines = DiffBuilder.BuildLineDiff(originalContent, proposedContent);
        diffHunks = DiffBuilder.GroupIntoHunks(diffLines);
    }

    partial void OnEditedContentChanged(string value)
    {
        IsDirty = value != ProposedContent;
        DiffLines = DiffBuilder.BuildLineDiff(OriginalContent, value);
        RevertToOriginalCommand.NotifyCanExecuteChanged();
    }

    partial void OnOriginalContentChanged(string value)
    {
        DiffLines = DiffBuilder.BuildLineDiff(value, EditedContent);
        RevertToOriginalCommand.NotifyCanExecuteChanged();
    }

    partial void OnDiffLinesChanged(IReadOnlyList<DiffLine> value) => DiffHunks = DiffBuilder.GroupIntoHunks(value);

    partial void OnDiffHunksChanged(IReadOnlyList<DiffHunk> value) => UpdateMatchedDiffHunks();

    private void UpdateMatchedDiffHunks()
    {
        foreach (var hunk in hunks)
        {
            hunk.MatchedDiffHunks = ProposedHunkMatcher.FindMatches(DiffHunks, hunk.Location);
        }
    }

    // IsDirty only ever changes as a side effect of EditedContent changing (above), but hooking its
    // own partial method - rather than calling NotifyCanExecuteChanged from OnEditedContentChanged
    // directly - keeps RevertToProposedCommand correct even if something else ever sets IsDirty.
    partial void OnIsDirtyChanged(bool value) => RevertToProposedCommand.NotifyCanExecuteChanged();

    // Discards the user's edits, falling back to what the LLM proposed. Disabled once there's
    // nothing to discard (EditedContent already equals ProposedContent).
    private bool CanRevertToProposed() => IsDirty;

    [RelayCommand(CanExecute = nameof(CanRevertToProposed))]
    private void RevertToProposed() => EditedContent = ProposedContent;

    // Discards both the user's edits and the LLM's proposed change, falling back to the file's
    // content on disk today. Enabled whenever there's anything to discard back to - including the
    // not-dirty case where the user hasn't touched the editor but still wants to reject the whole
    // proposed change.
    private bool CanRevertToOriginal() => EditedContent != OriginalContent;

    [RelayCommand(CanExecute = nameof(CanRevertToOriginal))]
    private void RevertToOriginal() => EditedContent = OriginalContent;
}
