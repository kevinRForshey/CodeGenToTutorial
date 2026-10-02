using Avalonia.Controls;
using Avalonia.Interactivity;

using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Avalonia.Views;

public partial class DiffsDetailControl : UserControl
{
    public DiffsDetailControl()
    {
        InitializeComponent();
    }

    private async void OnCopyButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProposedFileChange proposedFileChange)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(proposedFileChange.Content);
        }
    }
}
