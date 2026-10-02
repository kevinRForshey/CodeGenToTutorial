using Avalonia;
using Avalonia.Controls;

namespace CodeGenToTutorial.Avalonia.Helpers;

// Helper class to set the navigation target for a NavigationViewItem.
//
// Usage in XAML:
// <ui:NavigationViewItem Content="Main" helpers:NavigationHelper.NavigateTo="CodeGenToTutorial.Avalonia.ViewModels.MainViewModel" />
//
// Usage in code:
// NavigationHelper.SetNavigateTo(navigationViewItem, typeof(MainViewModel).FullName);
public static class NavigationHelper
{
    public static readonly AttachedProperty<string?> NavigateToProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("NavigateTo", typeof(NavigationHelper));

    public static string? GetNavigateTo(Control item) => item.GetValue(NavigateToProperty);

    public static void SetNavigateTo(Control item, string? value) => item.SetValue(NavigateToProperty, value);
}
