using Avalonia.Controls;

using CodeGenToTutorial.Avalonia.Strings;

namespace CodeGenToTutorial.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "AppDisplayName".GetLocalized();
    }
}
