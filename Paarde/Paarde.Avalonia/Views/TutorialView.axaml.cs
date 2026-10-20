using Avalonia.Controls;

using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Helpers;
using Paarde.Avalonia.Services;
using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class TutorialView : UserControl
{
    public TutorialViewModel ViewModel
    {
        get;
    }

    private readonly ResizablePaneHelper _paneHelper;

    public TutorialView()
    {
        ViewModel = App.GetService<TutorialViewModel>();
        DataContext = ViewModel;
        InitializeComponent();

        _paneHelper = new ResizablePaneHelper(WorkspaceGrid.ColumnDefinitions[0], App.GetService<ILocalSettingsService>(), WorkspaceLayoutSettingsKeys.TutorialFileListWidth, defaultWidth: 280);
        Loaded += async (_, _) => await _paneHelper.RestoreAsync();
        FileListSplitter.DragCompleted += (_, _) => _paneHelper.OnSplitterDragCompleted();
    }
}
