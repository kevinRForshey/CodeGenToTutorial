using Avalonia.Controls;

using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class HistoryView : UserControl
{
    public HistoryViewModel ViewModel
    {
        get;
    }

    public HistoryView()
    {
        ViewModel = App.GetService<HistoryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
