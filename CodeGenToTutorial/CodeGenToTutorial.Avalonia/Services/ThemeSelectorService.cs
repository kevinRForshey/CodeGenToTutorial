using Avalonia;
using Avalonia.Styling;

using CodeGenToTutorial.Avalonia.Contracts.Services;

namespace CodeGenToTutorial.Avalonia.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string SettingsKey = "AppBackgroundRequestedTheme";

    public ThemeVariant Theme { get; set; } = ThemeVariant.Default;

    private readonly ILocalSettingsService _localSettingsService;

    public ThemeSelectorService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync()
    {
        Theme = await LoadThemeFromSettingsAsync();
        await Task.CompletedTask;
    }

    public async Task SetThemeAsync(ThemeVariant theme)
    {
        Theme = theme;

        await SetRequestedThemeAsync();
        await SaveThemeInSettingsAsync(Theme);
    }

    public async Task SetRequestedThemeAsync()
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = Theme;
        }

        await Task.CompletedTask;
    }

    private async Task<ThemeVariant> LoadThemeFromSettingsAsync()
    {
        var themeName = await _localSettingsService.ReadSettingAsync<string>(SettingsKey);

        return themeName switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private async Task SaveThemeInSettingsAsync(ThemeVariant theme)
    {
        var name = theme == ThemeVariant.Light ? "Light" : theme == ThemeVariant.Dark ? "Dark" : "Default";
        await _localSettingsService.SaveSettingAsync(SettingsKey, name);
    }
}
