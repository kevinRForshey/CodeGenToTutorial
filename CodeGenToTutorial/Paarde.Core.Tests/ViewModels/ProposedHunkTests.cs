using Paarde.Core.ViewModels;

namespace Paarde.Core.Tests.ViewModels;

public class ProposedHunkTests
{
    [Fact]
    public void IsApplied_DefaultsToFalse()
    {
        var hunk = new ProposedHunk("Step 1", string.Empty, "x", null);

        Assert.False(hunk.IsApplied);
    }

    [Fact]
    public void IsApplied_SetTrue_ThenIsSelectedToggled_ResetsToFalse()
    {
        var hunk = new ProposedHunk("Step 1", string.Empty, "x", null)
        {
            IsApplied = true,
        };

        hunk.IsSelected = false;

        Assert.False(hunk.IsApplied);
    }

    [Fact]
    public void IsApplied_SetTrue_ThenIsSelectedSetToSameValue_IsUnaffected()
    {
        // CommunityToolkit.Mvvm's generated setter skips OnIsSelectedChanged when the new value
        // equals the current one, so re-assigning the same selection shouldn't invalidate IsApplied -
        // nothing about what's on disk actually became stale.
        var hunk = new ProposedHunk("Step 1", string.Empty, "x", null)
        {
            IsApplied = true,
        };

        hunk.IsSelected = true;

        Assert.True(hunk.IsApplied);
    }
}
