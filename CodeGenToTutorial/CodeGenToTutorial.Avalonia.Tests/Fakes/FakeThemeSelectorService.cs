using Avalonia.Styling;

using CodeGenToTutorial.Avalonia.Contracts.Services;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakeThemeSelectorService : IThemeSelectorService
{
    public ThemeVariant Theme { get; private set; } = ThemeVariant.Default;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task SetThemeAsync(ThemeVariant theme)
    {
        Theme = theme;
        return Task.CompletedTask;
    }

    public Task SetRequestedThemeAsync() => Task.CompletedTask;
}
