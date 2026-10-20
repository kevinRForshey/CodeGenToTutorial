using Avalonia.Controls;

using Paarde.Avalonia.Helpers;
using Paarde.Avalonia.Tests.Fakes;

namespace Paarde.Avalonia.Tests.Helpers;

public class ResizablePaneHelperTests
{
    private readonly FakeLocalSettingsService _settingsService = new();
    private readonly ColumnDefinition _column = new();

    private ResizablePaneHelper CreateSut(double defaultWidth = 280) =>
        new(_column, _settingsService, "Layout.Test.Width", defaultWidth);

    [Fact]
    public async Task RestoreAsync_NothingPersisted_UsesTheDefaultWidth()
    {
        var sut = CreateSut(defaultWidth: 280);

        await sut.RestoreAsync();

        Assert.Equal(280, _column.Width.Value);
    }

    [Fact]
    public async Task RestoreAsync_WidthPreviouslyPersisted_RestoresIt()
    {
        await _settingsService.SaveSettingAsync("Layout.Test.Width", 412.5);
        var sut = CreateSut(defaultWidth: 280);

        await sut.RestoreAsync();

        Assert.Equal(412.5, _column.Width.Value);
    }

    [Fact]
    public async Task RestoreAsync_PersistedWidthIsNegative_ClampsToZero()
    {
        // Shouldn't happen from dragging a GridSplitter, but guards against a corrupted
        // settings file producing a Grid with a nonsensical negative column width.
        await _settingsService.SaveSettingAsync("Layout.Test.Width", -50d);
        var sut = CreateSut();

        await sut.RestoreAsync();

        Assert.Equal(0, _column.Width.Value);
    }

    [Fact]
    public async Task OnSplitterDragCompleted_PersistsTheColumnsCurrentWidth()
    {
        var sut = CreateSut();
        _column.Width = new GridLength(123);

        sut.OnSplitterDragCompleted();
        // OnSplitterDragCompleted is async void (a UI event handler signature) - give its one
        // await (a synchronous FakeLocalSettingsService completion) a chance to run.
        await Task.Yield();

        Assert.Equal(123, await _settingsService.ReadSettingAsync<double?>("Layout.Test.Width"));
    }

    [Fact]
    public async Task OnSplitterDragCompleted_ColumnDraggedToZero_PersistsZero()
    {
        var sut = CreateSut();
        _column.Width = new GridLength(0);

        sut.OnSplitterDragCompleted();
        await Task.Yield();

        Assert.Equal(0, await _settingsService.ReadSettingAsync<double?>("Layout.Test.Width"));
    }
}
