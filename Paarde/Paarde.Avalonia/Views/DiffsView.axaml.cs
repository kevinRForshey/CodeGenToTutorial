using Avalonia.Controls;

using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class DiffsView : UserControl
{
    public DiffsViewModel ViewModel
    {
        get;
    }

    public DiffsView()
    {
        ViewModel = App.GetService<DiffsViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
