namespace CodeGenToTutorial.Models;

public class ProposedFileChange
{
    public string FilePath { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public IReadOnlyList<DiffLine> DiffLines { get; init; } = Array.Empty<DiffLine>();

    public IReadOnlyList<TutorialStep> TutorialSteps { get; init; } = Array.Empty<TutorialStep>();
}
