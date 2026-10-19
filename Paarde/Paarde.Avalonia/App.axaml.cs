using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Models;
using Paarde.Avalonia.Services;
using Paarde.Avalonia.ViewModels;
using Paarde.Avalonia.Views;
using Paarde.Core.Contracts.Services;
using Paarde.Core.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Paarde.Avalonia;

public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.axaml.cs.");
        }

        return service;
    }

    public static Window? MainWindow
    {
        get; private set;
    }

    public App()
    {
        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddSingleton<ClaudeCliOptionsProvider>();
            services.AddSingleton<ILlmRunner>(sp => new ClaudeCliRunner(sp.GetRequiredService<ClaudeCliOptionsProvider>().GetOptionsAsync));
            services.AddTransient<IFolderPickerService, FolderPickerService>();
            services.AddTransient<IFilePickerService, FilePickerService>();
            services.AddTransient<IFileSavePickerService, FileSavePickerService>();
            services.AddSingleton<IPromptResultStore, PromptResultStore>();
            services.AddSingleton<IPromptTemplateProvider, PromptTemplateProvider>();
            services.AddTransient<IFileChangeApplyService, FileChangeApplyService>();
            services.AddSingleton<IApplyBackupStore, ApplyBackupStore>();
            services.AddSingleton<IApplyHistoryStore, ApplyHistoryStore>();
            services.AddSingleton<IApplyHistoryLog, ApplyHistoryLog>();

            // T7.1.1: persists each run to %AppData%/<ApplicationDataFolder>/Sessions/*.json - reads
            // the same LocalSettingsOptions.ApplicationDataFolder LocalSettingsService uses (rather
            // than hardcoding a second copy of that folder name) so a user-configured data folder
            // covers sessions too.
            services.AddSingleton<IRunSessionStore>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<LocalSettingsOptions>>().Value;
                var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var applicationDataFolder = Path.Combine(localApplicationData, options.ApplicationDataFolder ?? "Paarde.Avalonia/ApplicationData");
                var sessionsFolder = Path.Combine(applicationDataFolder, "Sessions");

                return new RunSessionStore(
                    sp.GetRequiredService<IFileService>(),
                    sp.GetRequiredService<IPromptResultStore>(),
                    sp.GetRequiredService<IApplyHistoryLog>(),
                    sessionsFolder);
            });

            services.AddTransient<IGitStatusService, GitStatusService>();
            services.AddTransient<IApplyOrchestrationService, ApplyOrchestrationService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();
            services.AddTransient<IDialogService, DialogService>();

            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsView>();
            services.AddTransient<TutorialViewModel>();
            services.AddTransient<TutorialView>();
            services.AddTransient<HistoryViewModel>();
            services.AddTransient<HistoryView>();
            services.AddTransient<DiffsViewModel>();
            services.AddTransient<DiffsView>();
            services.AddTransient<PromptViewModel>();
            services.AddTransient<PromptView>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainView>();
            services.AddTransient<ShellView>();
            services.AddTransient<ShellViewModel>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // desktop.MainWindow must be assigned synchronously before this method returns: Avalonia's
            // desktop lifetime shows/tracks whatever window is assigned at that point, so window creation
            // can't sit behind an await. Theme init/navigation don't need to block that, so they run after.
            var shell = GetService<ShellView>();
            var window = new Views.MainWindow { Content = shell };
            MainWindow = window;
            desktop.MainWindow = window;

            InitializeThemeAndNavigate();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void InitializeThemeAndNavigate()
    {
        var themeSelectorService = GetService<IThemeSelectorService>();
        await themeSelectorService.InitializeAsync();
        await themeSelectorService.SetRequestedThemeAsync();

        // T7.1.2: pushes the last recorded run's tutorial/files/apply-log into IPromptResultStore/
        // IApplyHistoryLog before anything navigates, so the Tutorial and Diffs pages already have
        // it whenever the user first visits them - a no-op if no run has ever been recorded.
        GetService<IRunSessionStore>().RestoreLastSession();

        GetService<INavigationService>().NavigateTo(typeof(MainViewModel).FullName!);
    }
}
