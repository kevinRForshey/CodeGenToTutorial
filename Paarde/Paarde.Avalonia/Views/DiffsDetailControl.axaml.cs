using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Paarde.Core.Helpers;
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

        // T8.3.1: recomputes the side-by-side rows whenever the file being shown changes (switching
        // files reuses this one control instance) or that file's DiffLines changes (an edit, or
        // OriginalContent refreshing from disk) - kept up to date even while the inline view is the
        // one currently visible, so toggling to side-by-side never shows stale rows.
        DataContextChanged += (_, _) => OnDataContextChanged();
        OnDataContextChanged();
    }

    private ProposedFileChangeViewModel? _subscribedViewModel;

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

    // T8.3.1: Inline/side-by-side is local UI state (not persisted, not on the shared Core view
    // model, which multiple pages bind to) - a ToggleButton rather than a settings-backed flag,
    // same "no separate state to keep in sync" reasoning as T8.2.3's collapsible panes just
    // applied to a two-way local switch instead.
    private void OnViewModeToggled(object? sender, RoutedEventArgs e)
    {
        var isSideBySide = SideBySideToggle.IsChecked == true;
        DiffLinesItemsControl.IsVisible = !isSideBySide;
        SideBySideScrollViewer.IsVisible = isSideBySide;
    }

    private void OnDataContextChanged()
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = DataContext as ProposedFileChangeViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateSideBySideRows();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProposedFileChangeViewModel.DiffLines))
        {
            UpdateSideBySideRows();
        }
    }

    private void UpdateSideBySideRows() =>
        SideBySideItemsControl.ItemsSource = _subscribedViewModel is null
            ? null
            : DiffBuilder.BuildSideBySideRows(_subscribedViewModel.DiffLines);

    // T8.4.5: scrolls to the hunk a DiffJumpRequest targeted, if any. SourceRangeStart is the
    // target hunk's first line's index in DiffLines, which is exactly the flat list DiffLinesItemsControl
    // is bound to - no separate index lookup needed. Always switches back to inline first - the
    // side-by-side view has no equivalent index-based scroll target, and jumping here from a
    // tutorial step/the diff list is common enough that silently landing on the wrong mode would
    // be confusing.
    private void ApplyPendingScrollTarget()
    {
        if (this.FindAncestorOfType<DiffsView>()?.ViewModel.TakePendingScrollTarget() is { } target)
        {
            SideBySideToggle.IsChecked = false;
            DiffLinesItemsControl.IsVisible = true;
            SideBySideScrollViewer.IsVisible = false;
            DiffLinesItemsControl.ScrollIntoView(target.SourceRangeStart);
        }
    }
}
