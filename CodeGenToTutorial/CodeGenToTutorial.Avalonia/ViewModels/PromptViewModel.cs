using System.Diagnostics;

using Avalonia.Threading;

using CodeGenToTutorial.Avalonia.Contracts.Services;
using CodeGenToTutorial.Avalonia.Contracts.ViewModels;
using CodeGenToTutorial.Avalonia.Services;
using CodeGenToTutorial.Avalonia.Strings;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.Avalonia.ViewModels;

public partial class PromptViewModel : ObservableRecipient, INavigationAware
{
    private const string PromptSettingsKey = "SavedPrompt";
    private const string WorkingDirectorySettingsKey = "SavedWorkingDirectory";
    private const string TimeoutSecondsSettingsKey = "PromptTimeoutSeconds";
    private const int DefaultTimeoutSeconds = 300;

    // Hard-coded instructions prepended to every prompt sent to Claude Code. Refine as the tutorial/diff workflow evolves.
    private const string TutorialInstructionPrompt =
        """
        You are being called from a desktop app. First decide what kind of request the user made below:

        1. General question (math, trivia, "explain X", anything that does not require changing files in this
           codebase): just answer it directly and concisely. Do not produce a tutorial or file list for these.

        2. Development request (asks for a feature, fix, or change to this codebase): do NOT modify any files.
           Instead respond with exactly two top-level sections, using these headers so they can be parsed:

           ## Tutorial
           A short paragraph summarizing the overall change.

           ## Files
           For every file that needs to change, add a subsection formatted exactly like this, repeated for each
           file:

           ### File: <path to the file, relative to the working directory>
           ```
           <the complete contents of the file with the proposed changes applied - the whole file, not a diff>
           ```

           #### Tutorial
           Break down why and how this file is changing into one or more ordered steps, each formatted exactly
           like this:

           ##### Step: <short title for this part of the change>
           One or more paragraphs explaining, in detail, the reasoning behind this specific part of the change
           and how it works, written so someone learning to code with an LLM can follow along.
           ```
           <only the new or changed code that this step introduces, not the whole file>
           ```

           Repeat the "##### Step" block for every distinct part of the file's change.

           Do not put a language tag after the three backticks. If no existing files need changes, write
           "No files need changes." instead of any file subsections.

        Only use the Development request format when the request actually requires code changes. Here is the
        user's request:
        """ + "\n\n";

    private readonly IClaudeCliService _claudeCliService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IPromptResultStore _promptResultStore;

    [ObservableProperty]
    private string promptText = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string cliOutput = string.Empty;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string workingDirectoryPath = string.Empty;

    [ObservableProperty]
    private int timeoutSeconds = DefaultTimeoutSeconds;

    // T6.2.3: live "Xs" readout ticking while a run is in progress, and a post-run cost/token
    // summary once the CLI's final result line has been parsed.
    [ObservableProperty]
    private string elapsedTimeDisplay = string.Empty;

    [ObservableProperty]
    private string usageSummary = string.Empty;

    // T6.1.1: lives only for the duration of one RunPromptAsync call, so CancelPromptCommand has
    // something to cancel and the run's own cleanup has something to dispose - null the rest of
    // the time (including CanCancelPrompt's guard) rather than relying solely on IsRunning, in case
    // a run finishes and clears it in the same moment the user clicks Cancel.
    private CancellationTokenSource? _runCancellationTokenSource;

    // T6.1.2: cancels independently of _runCancellationTokenSource so RunPromptAsync's catch block
    // can tell *why* the run stopped - checking which of the two sources actually requested
    // cancellation - rather than guessing from a single shared token.
    private CancellationTokenSource? _timeoutCancellationTokenSource;

    // T6.2.3: ticks ElapsedTimeDisplay once a second while a run is in progress.
    private DispatcherTimer? _elapsedTimer;

    private Stopwatch? _runStopwatch;

    public PromptViewModel(IClaudeCliService claudeCliService, ILocalSettingsService localSettingsService, IFolderPickerService folderPickerService, IPromptResultStore promptResultStore)
    {
        _claudeCliService = claudeCliService;
        _localSettingsService = localSettingsService;
        _folderPickerService = folderPickerService;
        _promptResultStore = promptResultStore;
    }

    public async void OnNavigatedTo(object parameter)
    {
        PromptText = await _localSettingsService.ReadSettingAsync<string>(PromptSettingsKey) ?? string.Empty;
        WorkingDirectoryPath = await _localSettingsService.ReadSettingAsync<string>(WorkingDirectorySettingsKey) ?? string.Empty;

        var savedTimeoutSeconds = await _localSettingsService.ReadSettingAsync<int>(TimeoutSecondsSettingsKey);
        TimeoutSeconds = savedTimeoutSeconds > 0 ? savedTimeoutSeconds : DefaultTimeoutSeconds;

        StatusMessage = string.Empty;
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private async Task SavePromptAsync()
    {
        await _localSettingsService.SaveSettingAsync(PromptSettingsKey, PromptText);

        StatusMessage = "Prompt_Saved".GetLocalized();
    }

    [RelayCommand]
    private async Task BrowseWorkingDirectoryAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync();

        if (folder != null)
        {
            WorkingDirectoryPath = folder;
            await _localSettingsService.SaveSettingAsync(WorkingDirectorySettingsKey, WorkingDirectoryPath);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunPrompt))]
    private async Task RunPromptAsync()
    {
        var cliOptions = await LoadClaudeCliOptionsAsync();

        if (!_claudeCliService.IsAvailable(cliOptions))
        {
            StatusMessage = "Prompt_ClaudeCliNotFound".GetLocalized();
            return;
        }

        IsRunning = true;
        CliOutput = string.Empty;
        UsageSummary = string.Empty;
        StatusMessage = "Prompt_Running".GetLocalized();

        _runCancellationTokenSource = new CancellationTokenSource();
        _timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        CancelPromptCommand.NotifyCanExecuteChanged();

        _runStopwatch = Stopwatch.StartNew();
        ElapsedTimeDisplay = "0s";
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) => ElapsedTimeDisplay = $"{(int)_runStopwatch.Elapsed.TotalSeconds}s";
        _elapsedTimer.Start();

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _runCancellationTokenSource.Token, _timeoutCancellationTokenSource.Token);

        try
        {
            var fullPrompt = TutorialInstructionPrompt + PromptText;

            if (!string.IsNullOrWhiteSpace(WorkingDirectoryPath))
            {
                fullPrompt += $"\n\nThe working directory is: {WorkingDirectoryPath}";
            }

            // T6.2.1/T6.2.2: fires on a background thread as the CLI streams assistant text, so UI
            // state is only ever touched after marshaling onto the UI thread. Re-parsing the
            // growing CliOutput on every delta is safe because ClaudeResponseParser only matches
            // complete (closed-fence) sections - partial/in-progress blocks are naturally ignored
            // until they finish, so this surfaces newly-completed files as soon as they're emitted
            // instead of waiting for the whole run to finish.
            void OnTextDelta(string text) => Dispatcher.UIThread.Post(() =>
            {
                CliOutput += text;

                var streamedSoFar = ClaudeResponseParser.Parse(CliOutput);
                _promptResultStore.SetResult(streamedSoFar.Tutorial, streamedSoFar.Files, WorkingDirectoryPath);
            });

            var result = await _claudeCliService.RunPromptAsync(fullPrompt, WorkingDirectoryPath, cliOptions, OnTextDelta, linkedCancellationTokenSource.Token);

            CliOutput = result.ExitCode == 0 || string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

            if (result.TotalCostUsd.HasValue)
            {
                UsageSummary = string.Format(
                    "Prompt_UsageSummary".GetLocalized(),
                    result.TotalCostUsd.Value,
                    result.InputTokens ?? 0,
                    result.OutputTokens ?? 0,
                    (result.DurationMs ?? 0) / 1000.0);
            }

            if (result.ExitCode == 0)
            {
                var parsed = ClaudeResponseParser.Parse(CliOutput);
                _promptResultStore.SetResult(parsed.Tutorial, parsed.Files, WorkingDirectoryPath);

                StatusMessage = parsed.Files.Count > 0
                    ? string.Format("Prompt_RunCompletedWithFiles".GetLocalized(), parsed.Files.Count)
                    : "Prompt_RunCompleted".GetLocalized();
            }
            else
            {
                StatusMessage = string.Format("Prompt_RunFailed".GetLocalized(), result.ExitCode);
            }
        }
        catch (OperationCanceledException)
        {
            // Both sources funnel into the same linked token, so the only way to tell a timeout
            // apart from a user-initiated cancel (T6.1.2) is which underlying source actually
            // requested it.
            StatusMessage = _timeoutCancellationTokenSource.IsCancellationRequested
                ? string.Format("Prompt_RunTimedOut".GetLocalized(), TimeoutSeconds)
                : "Prompt_RunCancelled".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("Prompt_RunError".GetLocalized(), ex.Message);
        }
        finally
        {
            IsRunning = false;
            _runCancellationTokenSource?.Dispose();
            _runCancellationTokenSource = null;
            _timeoutCancellationTokenSource?.Dispose();
            _timeoutCancellationTokenSource = null;
            _elapsedTimer?.Stop();
            _elapsedTimer = null;
            _runStopwatch = null;
            CancelPromptCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelPrompt))]
    private void CancelPrompt() => _runCancellationTokenSource?.Cancel();

    private bool CanRunPrompt() => !IsRunning && !string.IsNullOrWhiteSpace(PromptText);

    private bool CanCancelPrompt() => _runCancellationTokenSource is not null;

    partial void OnPromptTextChanged(string value) => RunPromptCommand.NotifyCanExecuteChanged();

    partial void OnIsRunningChanged(bool value) => RunPromptCommand.NotifyCanExecuteChanged();

    partial void OnTimeoutSecondsChanged(int value) => _localSettingsService.SaveSettingAsync(TimeoutSecondsSettingsKey, value);

    // T6.1.4: reads the Settings page's CLI path/arguments on every run rather than caching them,
    // so a change made there takes effect on the next run without requiring a page revisit here.
    private async Task<ClaudeCliOptions> LoadClaudeCliOptionsAsync()
    {
        var executablePath = await _localSettingsService.ReadSettingAsync<string>(ClaudeCliSettingsKeys.ExecutablePath);
        var arguments = await _localSettingsService.ReadSettingAsync<string>(ClaudeCliSettingsKeys.Arguments);

        return new ClaudeCliOptions(
            string.IsNullOrWhiteSpace(executablePath) ? ClaudeCliOptions.Default.ExecutablePath : executablePath,
            string.IsNullOrWhiteSpace(arguments) ? ClaudeCliOptions.Default.Arguments : arguments);
    }
}
