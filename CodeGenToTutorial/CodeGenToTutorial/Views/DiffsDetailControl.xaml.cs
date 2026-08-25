using CodeGenToTutorial.Models;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.ApplicationModel.DataTransfer;

namespace CodeGenToTutorial.Views;

public sealed partial class DiffsDetailControl : UserControl
{
    public ProposedFileChange? ProposedFileChange
    {
        get => GetValue(ProposedFileChangeProperty) as ProposedFileChange;
        set => SetValue(ProposedFileChangeProperty, value);
    }

    public static readonly DependencyProperty ProposedFileChangeProperty = DependencyProperty.Register(nameof(ProposedFileChange), typeof(ProposedFileChange), typeof(DiffsDetailControl), new PropertyMetadata(null));

    public DiffsDetailControl()
    {
        InitializeComponent();
    }

    private void OnCopyButtonClick(object sender, RoutedEventArgs e)
    {
        if (ProposedFileChange == null)
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(ProposedFileChange.Content);
        Clipboard.SetContent(dataPackage);
    }
}
