using Avalonia.Controls;
using Avalonia.Interactivity;

using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class DiffsDetailControl : UserControl
{
    public DiffsDetailControl()
    {
        InitializeComponent();
    }

    private async void OnCopyButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProposedFileChangeViewModel proposedFileChange)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(proposedFileChange.ProposedContent);
        }
    }
}
