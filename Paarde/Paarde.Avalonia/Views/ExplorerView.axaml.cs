using Avalonia.Controls;

using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class ExplorerView : UserControl
{
    public ExplorerViewModel ViewModel
    {
        get;
    }

    public ExplorerView()
    {
        ViewModel = App.GetService<ExplorerViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
