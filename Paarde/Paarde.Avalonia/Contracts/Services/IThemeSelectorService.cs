using Avalonia.Styling;

namespace Paarde.Avalonia.Contracts.Services;

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
