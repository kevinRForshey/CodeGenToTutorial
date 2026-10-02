using Avalonia.Controls;

using CodeGenToTutorial.Avalonia.ViewModels;

namespace CodeGenToTutorial.Avalonia.Views;

public partial class ShellView : UserControl
{
    public ShellViewModel ViewModel
    {
        get;
    }

    public ShellView()
    {
        ViewModel = App.GetService<ShellViewModel>();
        DataContext = ViewModel;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);
    }
}
