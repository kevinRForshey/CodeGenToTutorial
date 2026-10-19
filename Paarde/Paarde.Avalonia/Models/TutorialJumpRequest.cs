using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Models;

// Navigation parameter from the Diffs page's "Back to tutorial" action to the Tutorial page
// (T8.4.5), returning to the step a DiffJumpRequest originally jumped from.
public record TutorialJumpRequest(ProposedFileChangeViewModel File, ProposedHunk TargetStep);
