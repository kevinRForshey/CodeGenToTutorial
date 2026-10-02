using Avalonia.Styling;

namespace CodeGenToTutorial.Avalonia.Contracts.Services;

public interface IThemeSelectorService
{
    ThemeVariant Theme
    {
        get;
    }

    Task InitializeAsync();

    Task SetThemeAsync(ThemeVariant theme);

    Task SetRequestedThemeAsync();
}
