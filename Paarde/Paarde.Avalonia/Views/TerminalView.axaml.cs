using System.ComponentModel;

using Avalonia.Controls;

using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Views;

public partial class TerminalView : UserControl
{
    public TerminalViewModel ViewModel
    {
        get;
    }

    public TerminalView()
    {
        ViewModel = App.GetService<TerminalViewModel>();
        DataContext = ViewModel;
        InitializeComponent();

        // T16.1.3: a plain TextBox doesn't auto-scroll as bound text grows - moving the caret to
        // the end on every OutputText change keeps the latest streamed output visible, the same
        // "always show the tail" behavior a real terminal has.
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalViewModel.OutputText))
        {
            OutputTextBox.CaretIndex = OutputTextBox.Text?.Length ?? 0;
        }
    }
}
