using Paarde.Core.ViewModels;

namespace Paarde.Core.Models;

public record ClaudeParsedResponse(string Tutorial, IReadOnlyList<ProposedFileChangeViewModel> Files);
