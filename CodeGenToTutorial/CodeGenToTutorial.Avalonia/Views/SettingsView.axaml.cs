using Avalonia.Controls;

using CodeGenToTutorial.Avalonia.ViewModels;

namespace CodeGenToTutorial.Avalonia.Views;

public partial class SettingsView : UserControl
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    public SettingsView()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
