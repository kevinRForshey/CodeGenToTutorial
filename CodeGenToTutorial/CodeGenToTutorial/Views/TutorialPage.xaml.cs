using CodeGenToTutorial.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace CodeGenToTutorial.Views;

public sealed partial class TutorialPage : Page
{
    public TutorialViewModel ViewModel
    {
        get;
    }

    public TutorialPage()
    {
        ViewModel = App.GetService<TutorialViewModel>();
        InitializeComponent();
    }
}
