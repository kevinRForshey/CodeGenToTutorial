using Avalonia.Controls;

using CodeGenToTutorial.Avalonia.ViewModels;

namespace CodeGenToTutorial.Avalonia.Views;

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
