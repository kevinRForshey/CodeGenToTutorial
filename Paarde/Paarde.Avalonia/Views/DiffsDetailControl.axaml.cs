using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class DiffsDetailControl : UserControl
{
    public DiffsDetailControl()
    {
        InitializeComponent();

        // T8.4.5: applied once this control is attached to DiffsView's visual tree, by which point
        // DataContext (this control's file) is already set.
        Loaded += (_, _) => ApplyPendingScrollTarget();
    }

    private async void OnCopyButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProposedFileChangeViewModel proposedFileChange)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(proposedFileChange.ProposedContent);
        }
    }

    // T8.4.5: scrolls to the hunk a DiffJumpRequest targeted, if any. SourceRangeStart is the
    // target hunk's first line's index in DiffLines, which is exactly the flat list DiffLinesItemsControl
    // is bound to - no separate index lookup needed.
    private void ApplyPendingScrollTarget()
    {
        if (this.FindAncestorOfType<DiffsView>()?.ViewModel.TakePendingScrollTarget() is { } target)
        {
            DiffLinesItemsControl.ScrollIntoView(target.SourceRangeStart);
        }
    }
}
