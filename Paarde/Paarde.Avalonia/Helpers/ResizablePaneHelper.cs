using Avalonia.Controls;

using Paarde.Avalonia.Contracts.Services;

namespace Paarde.Avalonia.Helpers;

// T8.2.3: restores and persists one Grid column's width (a master/browse-list pane) so every
// master-detail page (Explorer, Diffs, Tutorial, History) shares the same GridSplitter +
// ILocalSettingsService plumbing instead of duplicating it per view. Dragging a pane all the way
// shut (width 0) and reopening it is this app's "collapsible" - there's no separate collapsed flag
// to keep in sync with the dragged width.
public sealed class ResizablePaneHelper
{
    private readonly ColumnDefinition _column;
    private readonly ILocalSettingsService _settingsService;
    private readonly string _widthSettingKey;
    private readonly double _defaultWidth;

    public ResizablePaneHelper(ColumnDefinition column, ILocalSettingsService settingsService, string widthSettingKey, double defaultWidth)
    {
        _column = column;
        _settingsService = settingsService;
        _widthSettingKey = widthSettingKey;
        _defaultWidth = defaultWidth;
    }

    public async Task RestoreAsync()
    {
        var width = await _settingsService.ReadSettingAsync<double?>(_widthSettingKey) ?? _defaultWidth;
        _column.Width = new GridLength(Math.Max(width, 0));
    }

    public async void OnSplitterDragCompleted() => await _settingsService.SaveSettingAsync(_widthSettingKey, _column.Width.Value);
}
