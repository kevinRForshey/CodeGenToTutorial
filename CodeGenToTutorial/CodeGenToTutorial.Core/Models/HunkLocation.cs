namespace CodeGenToTutorial.Core.Models;

// Zero-based, inclusive line range within a file's ProposedContent that a tutorial step's snippet
// resolves to.
public record HunkLocation(int StartLine, int EndLine);
