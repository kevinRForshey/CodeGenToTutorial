using System.Reflection;
using System.Windows.Input;

using Avalonia.Styling;

using CodeGenToTutorial.Avalonia.Contracts.Services;
using CodeGenToTutorial.Avalonia.Contracts.ViewModels;
using CodeGenToTutorial.Avalonia.Services;
using CodeGenToTutorial.Avalonia.Strings;
using CodeGenToTutorial.Core.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.Avalonia.ViewModels;

public partial class SettingsViewModel : ObservableRecipient, INavigationAware
{
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILocalSettingsService _localSettingsService;

    [ObservableProperty]
    private ThemeVariant _themeVariant;

    [ObservableProperty]
    private string _versionDescription;

    [ObservableProperty]
    private string claudeCliExecutablePath = ClaudeCliOptions.Default.ExecutablePath;

    [ObservableProperty]
    private string claudeCliArguments = ClaudeCliOptions.Default.Arguments;

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService)
    {
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        _themeVariant = _themeSelectorService.Theme;
        _versionDescription = GetVersionDescription();

        SwitchThemeCommand = new RelayCommand<ThemeVariant>(
            async (param) =>
            {
                if (param != null && ThemeVariant != param)
                {
                    ThemeVariant = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });
    }

    public async void OnNavigatedTo(object parameter)
    {
        var savedExecutablePath = await _localSettingsService.ReadSettingAsync<string>(ClaudeCliSettingsKeys.ExecutablePath);
        ClaudeCliExecutablePath = string.IsNullOrWhiteSpace(savedExecutablePath) ? ClaudeCliOptions.Default.ExecutablePath : savedExecutablePath;

        var savedArguments = await _localSettingsService.ReadSettingAsync<string>(ClaudeCliSettingsKeys.Arguments);
        ClaudeCliArguments = string.IsNullOrWhiteSpace(savedArguments) ? ClaudeCliOptions.Default.Arguments : savedArguments;
    }

    public void OnNavigatedFrom()
    {
    }

    partial void OnClaudeCliExecutablePathChanged(string value) => _localSettingsService.SaveSettingAsync(ClaudeCliSettingsKeys.ExecutablePath, value);

    partial void OnClaudeCliArgumentsChanged(string value) => _localSettingsService.SaveSettingAsync(ClaudeCliSettingsKeys.Arguments, value);

    private static string GetVersionDescription()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version!;

        return $"{"AppDisplayName".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
