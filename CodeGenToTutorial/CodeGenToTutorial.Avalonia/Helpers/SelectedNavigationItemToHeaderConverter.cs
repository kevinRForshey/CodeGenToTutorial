using System.Globalization;

using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace CodeGenToTutorial.Avalonia.Helpers;

// NavigationView.SelectedItem is the raw NavigationViewItem; the shell header should show that item's
// label (its Content), matching the original ShellPage's "((ContentControl)ViewModel.Selected).Content" bind.
public class SelectedNavigationItemToHeaderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as ContentControl)?.Content;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
