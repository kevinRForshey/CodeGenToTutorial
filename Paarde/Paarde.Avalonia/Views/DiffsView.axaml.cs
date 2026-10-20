using Avalonia.Controls;

using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Helpers;
using Paarde.Avalonia.Services;
using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class DiffsView : UserControl
{
    public DiffsViewModel ViewModel
    {
        get;
    }

    private readonly ResizablePaneHelper _paneHelper;

    public DiffsView()
    {
        ViewModel = App.GetService<DiffsViewModel>();
        DataContext = ViewModel;
        InitializeComponent();

        _paneHelper = new ResizablePaneHelper(WorkspaceGrid.ColumnDefinitions[0], App.GetService<ILocalSettingsService>(), WorkspaceLayoutSettingsKeys.DiffsFileListWidth, defaultWidth: 280);
        Loaded += async (_, _) => await _paneHelper.RestoreAsync();
        FileListSplitter.DragCompleted += (_, _) => _paneHelper.OnSplitterDragCompleted();
    }
}
