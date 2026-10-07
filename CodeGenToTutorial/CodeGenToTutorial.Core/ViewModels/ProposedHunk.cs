using CodeGenToTutorial.Core.Models;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeGenToTutorial.Core.ViewModels;

// Wraps a single parsed tutorial step as a selectable unit of change, carrying the resolved line
// range (if any) its snippet produces in the file. IsSelected is scaffolding for the hunk-level
// selection UI (F3.2) - nothing reads it yet, since apply still writes the whole file (T3.1.3).
public partial class ProposedHunk : ObservableObject
{
    public string Title { get; }

    public string Explanation { get; }

    public string Snippet { get; }

    public HunkLocation? Location { get; }

    [ObservableProperty]
    private bool isSelected = true;

    public ProposedHunk(string title, string explanation, string snippet, HunkLocation? location)
    {
        Title = title;
        Explanation = explanation;
        Snippet = snippet;
        Location = location;
    }
}
