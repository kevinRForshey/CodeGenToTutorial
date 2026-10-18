using Avalonia.Controls;

using Paarde.Avalonia.Strings;

namespace Paarde.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "AppDisplayName".GetLocalized();
    }
}
