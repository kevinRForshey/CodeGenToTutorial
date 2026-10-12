using CodeGenToTutorial.Core.Models;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeGenToTutorial.Core.ViewModels;

// Wraps a single parsed tutorial step as a selectable unit of change, carrying the resolved line
// range (if any) its snippet produces in the file. IsSelected drives the per-step checkbox (F3.2)
// and the owning ProposedFileChangeViewModel's tri-state file-level rollup; apply still writes the
// whole file regardless of selection until F3.3 wires selection into FileChangeApplyService.
public partial class ProposedHunk : ObservableObject
{
    public string Title { get; }

    public string Explanation { get; }

    public string Snippet { get; }

    public HunkLocation? Location { get; }

    [ObservableProperty]
    private bool isSelected = true;

    // The DiffHunk(s) (from ProposedFileChangeViewModel.DiffHunks) that this step's Location
    // overlaps - kept in sync by ProposedFileChangeViewModel whenever its DiffHunks are
    // recomputed. Empty when Location is unresolved or no diff hunk covers it (e.g. the step's
    // snippet turned out identical to what was already there).
    [ObservableProperty]
    private IReadOnlyList<DiffHunk> matchedDiffHunks = Array.Empty<DiffHunk>();

    public ProposedHunk(string title, string explanation, string snippet, HunkLocation? location)
    {
        Title = title;
        Explanation = explanation;
        Snippet = snippet;
        Location = location;
    }
}
