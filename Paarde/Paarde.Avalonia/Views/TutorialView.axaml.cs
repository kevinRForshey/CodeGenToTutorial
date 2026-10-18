using Avalonia.Controls;

using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class TutorialView : UserControl
{
    public TutorialViewModel ViewModel
    {
        get;
    }

    public TutorialView()
    {
        ViewModel = App.GetService<TutorialViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
