using Paarde.Core.Models;
using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Models;

// Navigation parameter from a tutorial step's "View in Diff" action to the Diffs page (T8.4.5).
// File is the shared ProposedFileChangeViewModel instance both pages read from IPromptResultStore,
// so the Diffs page can select it directly rather than searching its Files by path. OriginStep lets
// the Diffs page offer a "Back to tutorial" action that returns to the step the jump came from.
public record DiffJumpRequest(ProposedFileChangeViewModel File, DiffHunk TargetHunk, ProposedHunk OriginStep);
