using Avalonia.Controls;

using CodeGenToTutorial.Avalonia.ViewModels;

namespace CodeGenToTutorial.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainViewModel ViewModel
    {
        get;
    }

    public MainView()
    {
        ViewModel = App.GetService<MainViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
