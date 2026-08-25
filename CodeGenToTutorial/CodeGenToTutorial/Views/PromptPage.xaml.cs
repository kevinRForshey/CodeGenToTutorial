using CodeGenToTutorial.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace CodeGenToTutorial.Views;

public sealed partial class PromptPage : Page
{
    public PromptViewModel ViewModel
    {
        get;
    }

    public PromptPage()
    {
        ViewModel = App.GetService<PromptViewModel>();
        InitializeComponent();
    }
}
