using System.Reflection;
using System.Windows.Input;

using Avalonia.Styling;

using CodeGenToTutorial.Avalonia.Contracts.Services;
using CodeGenToTutorial.Avalonia.Contracts.ViewModels;
using CodeGenToTutorial.Avalonia.Services;
using CodeGenToTutorial.Avalonia.Strings;
using CodeGenToTutorial.Core.Contracts.Services;
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

    // T5.3.2: blank means "use DefaultPromptTemplate" - PromptViewModel applies that same fallback
    // when it reads this setting fresh on every run.
    [ObservableProperty]
    private string promptTemplateOverride = string.Empty;

    // The built-in instructions (CodeGenToTutorial.Core/Prompts/TutorialInstructionPromptV1.txt),
    // shown read-only so the user has something to compare their override against or copy from -
    // never itself persisted, so a future template version bump is picked up automatically.
    public string DefaultPromptTemplate
    {
        get;
    }

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService, IPromptTemplateProvider promptTemplateProvider)
    {
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        _themeVariant = _themeSelectorService.Theme;
        _versionDescription = GetVersionDescription();
        DefaultPromptTemplate = promptTemplateProvider.GetTutorialInstructionPrompt();

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

        PromptTemplateOverride = await _localSettingsService.ReadSettingAsync<string>(PromptTemplateSettingsKeys.Override) ?? string.Empty;
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand(CanExecute = nameof(CanResetPromptTemplate))]
    private void ResetPromptTemplate() => PromptTemplateOverride = string.Empty;

    private bool CanResetPromptTemplate() => !string.IsNullOrEmpty(PromptTemplateOverride);

    partial void OnClaudeCliExecutablePathChanged(string value) => _localSettingsService.SaveSettingAsync(ClaudeCliSettingsKeys.ExecutablePath, value);

    partial void OnClaudeCliArgumentsChanged(string value) => _localSettingsService.SaveSettingAsync(ClaudeCliSettingsKeys.Arguments, value);

    partial void OnPromptTemplateOverrideChanged(string value)
    {
        _localSettingsService.SaveSettingAsync(PromptTemplateSettingsKeys.Override, value);
        ResetPromptTemplateCommand.NotifyCanExecuteChanged();
    }

    private static string GetVersionDescription()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version!;

        return $"{"AppDisplayName".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
