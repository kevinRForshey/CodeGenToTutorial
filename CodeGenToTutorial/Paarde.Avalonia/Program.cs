using Avalonia;

namespace Paarde.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // DIAGNOSTIC (temporary, to be removed): catch anything that would otherwise crash silently
        // or terminate the process without a visible message.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            System.IO.File.AppendAllText("/tmp/nav-diag.log", $"UNHANDLED: {e.ExceptionObject}\n");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            System.IO.File.AppendAllText("/tmp/nav-diag.log", $"UNOBSERVED TASK: {e.Exception}\n");

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
