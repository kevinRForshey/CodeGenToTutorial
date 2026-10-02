using Avalonia.Controls;

using CodeGenToTutorial.Avalonia.ViewModels;

namespace CodeGenToTutorial.Avalonia.Views;

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
