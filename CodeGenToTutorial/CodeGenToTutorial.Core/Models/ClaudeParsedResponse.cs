namespace CodeGenToTutorial.Core.Models;

public record ClaudeParsedResponse(string Tutorial, IReadOnlyList<ProposedFileChange> Files);
