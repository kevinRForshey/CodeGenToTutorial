using CodeGenToTutorial.ViewModels;

using CommunityToolkit.WinUI.UI.Controls;

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

    private void OnViewStateChanged(object sender, ListDetailsViewState e)
    {
        if (e == ListDetailsViewState.Both)
        {
            ViewModel.EnsureItemSelected();
        }
    }
}
