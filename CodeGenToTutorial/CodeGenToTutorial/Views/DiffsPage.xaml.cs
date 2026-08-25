using CodeGenToTutorial.ViewModels;

using CommunityToolkit.WinUI.UI.Controls;

using Microsoft.UI.Xaml.Controls;

namespace CodeGenToTutorial.Views;

public sealed partial class DiffsPage : Page
{
    public DiffsViewModel ViewModel
    {
        get;
    }

    public DiffsPage()
    {
        ViewModel = App.GetService<DiffsViewModel>();
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
