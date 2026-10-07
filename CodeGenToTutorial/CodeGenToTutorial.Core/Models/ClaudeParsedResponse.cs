using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Models;

public record ClaudeParsedResponse(string Tutorial, IReadOnlyList<ProposedFileChangeViewModel> Files);
