using Avalonia.Controls;

using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class PromptView : UserControl
{
    public PromptViewModel ViewModel
    {
        get;
    }

    public PromptView()
    {
        ViewModel = App.GetService<PromptViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
