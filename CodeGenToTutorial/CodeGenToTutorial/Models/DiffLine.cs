namespace CodeGenToTutorial.Models;

public enum DiffLineKind
{
    Unchanged,
    Added,
    Removed,
}

public record DiffLine(DiffLineKind Kind, string Text)
{
    public string Prefix => Kind switch
    {
        DiffLineKind.Added => "+ ",
        DiffLineKind.Removed => "- ",
        _ => "  ",
    };
}
