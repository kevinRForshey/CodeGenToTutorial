using Avalonia.Threading;

using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Contracts.ViewModels;
using Paarde.Avalonia.Services;
using Paarde.Avalonia.Strings;
using Paarde.Core.Contracts.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Paarde.Avalonia.ViewModels;

// T16.1.2: the integrated terminal - a "run a command, stream its output" panel rather than a
// real PTY (see ROADMAP.md's F16.1 for why), reusing the working directory last picked on the
// Prompt page the same way ExplorerViewModel does (T8.1.1), via WorkingDirectorySettingsKeys.
public partial class TerminalViewModel : ObservableObject, INavigationAware
{
    private readonly ITerminalCommandRunner _terminalCommandRunner;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ILogger<TerminalViewModel> _logger;

    // T16.1.1: lives only for the duration of one RunCommandAsync call, same lifetime pattern as
    // PromptViewModel._runCancellationTokenSource - CancelCommandCommand's CanExecute guard is
    // whether one currently exists.
    private CancellationTokenSource? _runCancellationTokenSource;

    [ObservableProperty]
    private string workingDirectoryPath = string.Empty;

    [ObservableProperty]
    private string commandText = string.Empty;

    [ObservableProperty]
    private string outputText = string.Empty;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public TerminalViewModel(
        ITerminalCommandRunner terminalCommandRunner,
        ILocalSettingsService localSettingsService,
        ILogger<TerminalViewModel>? logger = null)
    {
        _terminalCommandRunner = terminalCommandRunner;
        _localSettingsService = localSettingsService;
        _logger = logger ?? NullLogger<TerminalViewModel>.Instance;
    }

    // T9.2.3: async void because INavigationAware.OnNavigatedTo returns void, same reasoning as
    // ExplorerViewModel.OnNavigatedTo - a corrupt settings file is logged rather than unwound
    // unobserved out of NavigationService.OnNavigated.
    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            WorkingDirectoryPath = await _localSettingsService.ReadSettingAsync<string>(WorkingDirectorySettingsKeys.WorkingDirectory) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the saved working directory while navigating to the Terminal page.");
        }
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task RunCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkingDirectoryPath))
        {
            StatusMessage = "Terminal_NoWorkingDirectory".Resolve();
            return;
        }

        IsRunning = true;
        OutputText = string.Empty;
        StatusMessage = string.Empty;
        _runCancellationTokenSource = new CancellationTokenSource();
        CancelCommandCommand.NotifyCanExecuteChanged();

        try
        {
            // T16.1.3: fires on a background thread as the process streams output, so UI state is
            // only ever touched after marshaling onto the UI thread - same requirement as
            // PromptViewModel.OnTextDelta.
            void OnOutputLine(string line) => Dispatcher.UIThread.Post(() => OutputText += line + Environment.NewLine);

            var result = await _terminalCommandRunner.RunAsync(CommandText, WorkingDirectoryPath, OnOutputLine, _runCancellationTokenSource.Token);

            StatusMessage = result.Succeeded
                ? string.Format("Terminal_CommandSucceeded".Resolve(), result.Duration.TotalSeconds)
                : string.Format("Terminal_CommandFailed".Resolve(), result.ExitCode, result.Duration.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Terminal_CommandCancelled".Resolve();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Failed to run a terminal command in {WorkingDirectory}.", WorkingDirectoryPath);
            StatusMessage = string.Format("Terminal_CommandRunError".Resolve(), ex.Message);
        }
        finally
        {
            IsRunning = false;
            _runCancellationTokenSource?.Dispose();
            _runCancellationTokenSource = null;
            CancelCommandCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunCommand() => !IsRunning && !string.IsNullOrWhiteSpace(CommandText);

    partial void OnCommandTextChanged(string value) => RunCommandCommand.NotifyCanExecuteChanged();

    partial void OnIsRunningChanged(bool value) => RunCommandCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanCancelCommand))]
    private void CancelCommand() => _runCancellationTokenSource?.Cancel();

    private bool CanCancelCommand() => _runCancellationTokenSource is not null;

    [RelayCommand]
    private void ClearOutput() => OutputText = string.Empty;
}
