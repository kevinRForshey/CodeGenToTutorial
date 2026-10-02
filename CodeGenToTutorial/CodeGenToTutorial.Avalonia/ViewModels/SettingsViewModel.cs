using System.Reflection;
using System.Windows.Input;

using Avalonia.Styling;

using CodeGenToTutorial.Avalonia.Contracts.Services;
using CodeGenToTutorial.Avalonia.Strings;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.Avalonia.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;

    [ObservableProperty]
    private ThemeVariant _themeVariant;

    [ObservableProperty]
    private string _versionDescription;

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService)
    {
        _themeSelectorService = themeSelectorService;
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

    private static string GetVersionDescription()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version!;

        return $"{"AppDisplayName".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
