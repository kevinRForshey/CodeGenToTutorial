using Avalonia.Controls;

using CodeGenToTutorial.Avalonia.ViewModels;

namespace CodeGenToTutorial.Avalonia.Views;

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
