namespace CodeGenToTutorial.Models;

public record ClaudeParsedResponse(string Tutorial, IReadOnlyList<ProposedFileChange> Files);
