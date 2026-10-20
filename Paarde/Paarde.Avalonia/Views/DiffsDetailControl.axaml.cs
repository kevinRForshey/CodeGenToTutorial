using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Paarde.Core.Helpers;
using Paarde.Core.Models;
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

        // T8.3.1/T8.3.2: recomputes both views' display rows whenever the file being shown changes
        // (switching files reuses this one control instance) or that file's DiffLines changes (an
        // edit, or OriginalContent refreshing from disk) - kept up to date even while the other view
        // is the one currently visible, so toggling modes never shows stale rows.
        DataContextChanged += (_, _) => OnDataContextChanged();
        OnDataContextChanged();
    }

    private ProposedFileChangeViewModel? _subscribedViewModel;

    // T8.3.2: which collapsed regions (keyed by CollapsibleDiffRow.LineIndex) the user has expanded
    // back to their real lines, for the file currently shown. Reset whenever DiffLines changes for
    // any reason (see OnDataContextChanged/OnViewModelPropertyChanged below) since a new diff means
    // those indices no longer necessarily point at the same regions.
    private readonly HashSet<int> _expandedSegments = [];

    // T8.3.2: the inline view's current rows, cached so ApplyPendingScrollTarget can look one up by
    // LineIndex rather than assuming a DiffLines index maps 1:1 onto a display row index.
    private IReadOnlyList<CollapsibleDiffRow> _inlineRows = [];

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

        // T8.3.2: switching files invalidates any expanded-region indices from the previous file's
        // DiffLines.
        _expandedSegments.Clear();

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RefreshDisplayRows();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProposedFileChangeViewModel.DiffLines))
        {
            // T8.3.2: an edit or an original-content refresh means DiffLines was recomputed from
            // scratch, so no previously expanded index is guaranteed to still refer to the same
            // region.
            _expandedSegments.Clear();
            RefreshDisplayRows();
        }
    }

    private void RefreshDisplayRows()
    {
        var diffLines = _subscribedViewModel?.DiffLines ?? Array.Empty<DiffLine>();

        _inlineRows = DiffBuilder.BuildCollapsibleRows(diffLines, _expandedSegments);
        DiffLinesItemsControl.ItemsSource = _inlineRows;
        SideBySideItemsControl.ItemsSource = DiffBuilder.BuildCollapsibleSideBySideRows(diffLines, _expandedSegments);
    }

    // T8.3.2: shared by both views' collapsed-region placeholders (CollapsibleDiffRow and
    // CollapsibleSideBySideDiffRow both implement ICollapsedDiffRegion) - expanding re-renders that
    // region's real lines in place rather than navigating anywhere.
    private void OnExpandCollapsedRegionClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is ICollapsedDiffRegion { IsCollapsedPlaceholder: true } region)
        {
            _expandedSegments.Add(region.LineIndex);
            RefreshDisplayRows();
        }
    }

    // T8.4.5: scrolls to the hunk a DiffJumpRequest targeted, if any. Always switches back to
    // inline first - the side-by-side view has no equivalent index-based scroll target, and jumping
    // here from a tutorial step/the diff list is common enough that silently landing on the wrong
    // mode would be confusing.
    private void ApplyPendingScrollTarget()
    {
        if (this.FindAncestorOfType<DiffsView>()?.ViewModel.TakePendingScrollTarget() is { } target)
        {
            SideBySideToggle.IsChecked = false;
            DiffLinesItemsControl.IsVisible = true;
            SideBySideScrollViewer.IsVisible = false;

            // T8.3.2: target.SourceRangeStart is an index into DiffLines, which no longer maps 1:1
            // onto a display row index once collapsing is involved - look the row up by LineIndex
            // instead. It's always present as its own row (never folded into a placeholder) because
            // BuildCollapsibleRows defaults to the same contextLines as GroupIntoHunks, so a hunk's
            // own leading context is exactly the context kept visible here.
            var displayIndex = -1;

            for (var i = 0; i < _inlineRows.Count; i++)
            {
                if (_inlineRows[i].LineIndex == target.SourceRangeStart)
                {
                    displayIndex = i;
                    break;
                }
            }

            if (displayIndex >= 0)
            {
                DiffLinesItemsControl.ScrollIntoView(displayIndex);
            }
        }
    }
}
