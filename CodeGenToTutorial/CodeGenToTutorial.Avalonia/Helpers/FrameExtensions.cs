using FluentAvalonia.UI.Controls;

namespace CodeGenToTutorial.Avalonia.Helpers;

public static class FrameExtensions
{
    public static object? GetPageViewModel(this Frame frame) => frame?.Content?.GetType().GetProperty("ViewModel")?.GetValue(frame.Content, null);
}
