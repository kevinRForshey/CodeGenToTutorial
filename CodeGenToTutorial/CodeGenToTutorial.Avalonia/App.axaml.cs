using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using CodeGenToTutorial.Avalonia.Contracts.Services;
using CodeGenToTutorial.Avalonia.Models;
using CodeGenToTutorial.Avalonia.Services;
using CodeGenToTutorial.Avalonia.ViewModels;
using CodeGenToTutorial.Avalonia.Views;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeGenToTutorial.Avalonia;

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
            services.AddSingleton<IClaudeCliService, ClaudeCliService>();
            services.AddTransient<IFolderPickerService, FolderPickerService>();
            services.AddSingleton<IPromptResultStore, PromptResultStore>();
            services.AddTransient<IFileChangeApplyService, FileChangeApplyService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsView>();
            services.AddTransient<TutorialViewModel>();
            services.AddTransient<TutorialView>();
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

        GetService<INavigationService>().NavigateTo(typeof(MainViewModel).FullName!);
    }
}
