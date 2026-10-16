using System.Diagnostics;

using Avalonia.Threading;

using CodeGenToTutorial.Avalonia.Contracts.Services;
using CodeGenToTutorial.Avalonia.Contracts.ViewModels;
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

    // Hard-coded instructions prepended to every prompt sent to Claude Code. Refine as the tutorial/diff
    // workflow evolves. Epic 5 / F5.1: the response contract is a single JSON object
    // (CodeGenToTutorial.Core.Models.ClaudeJsonResponse) instead of markdown headers, so it can be
    // parsed without depending on the model never reformatting a fence - see
    // CodeGenToTutorial.Core.Helpers.ClaudeJsonResponseParser. The old markdown parser
    // (ClaudeResponseParser) is kept only as a last-resort fallback for whatever the model actually
    // returns (T5.1.3), via StructuredResponseParser. F5.2: for a file that already exists, the model is
    // asked for "edits" (search/replace blocks) instead of its whole "content" - cutting tokens and
    // avoiding the whole-file-echo truncation failure mode (T5.2.1) - applied locally against the real
    // file by SearchReplaceEditApplier (T5.2.2); an edit whose "search" text doesn't match is treated as
    // a validation failure and fed back on the same one-shot retry as any other schema problem (T5.2.3).
    private const string TutorialInstructionPrompt =
        """
        You are being called from a desktop app. Respond with a single JSON object only - no markdown
        fences, no commentary before or after it - matching exactly one of these two shapes, depending on
        what kind of request the user made below:

        1. General question (math, trivia, "explain X", anything that does not require changing files in
           this codebase):
           {
             "responseType": "answer",
             "answer": "<your answer, as plain text>"
           }

        2. Development request (asks for a feature, fix, or change to this codebase) - do NOT modify any
           files yourself:
           {
             "responseType": "development",
             "tutorial": "<a short paragraph summarizing the overall change>",
             "files": [
               {
                 "path": "<path to the file, relative to the working directory>",
                 "edits": [
                   {
                     "title": "<short title for this part of the change>",
                     "explanation": "<one or more paragraphs explaining, in detail, the reasoning behind this specific part of the change and how it works, written so someone learning to code with an LLM can follow along>",
                     "search": "<the exact lines currently in the file that this step changes, copied verbatim including whitespace - include enough surrounding context that this text appears only once in the file>",
                     "replace": "<the new lines that replace \"search\" - use an empty string to delete \"search\" with nothing>"
                   }
                 ],
                 "content": "<only for a file that does not exist yet: its complete contents>"
               }
             ]
           }
           Use "edits" for every change to a file that already exists in the working directory - never
           repeat a whole file's contents just to change a few lines of it. Use "content" only when
           creating a brand-new file, and never include both "edits" and "content" for the same file.
           Repeat the "edits" entries for every distinct part of a file's change. If no files need
           changes, set "files" to an empty array.

        Only use the "development" shape when the request actually requires code changes. Every string
        value must be valid JSON (escape newlines, quotes, and backslashes). Here is the user's request:
        """ + "\n\n";

    private const string RetryInstructionSuffix =
        "\n\nYour previous response could not be parsed: {0}\n\nRespond again with ONLY a single corrected JSON object matching the schema above - no markdown fences, no commentary.";

    private readonly ILlmRunner _llmRunner;
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

    public PromptViewModel(ILlmRunner llmRunner, ILocalSettingsService localSettingsService, IFolderPickerService folderPickerService, IPromptResultStore promptResultStore)
    {
        _llmRunner = llmRunner;
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
        if (!await _llmRunner.IsAvailableAsync())
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
            // state is only ever touched after marshaling onto the UI thread. Re-parsing the growing
            // CliOutput on every delta with the legacy markdown parser is a harmless no-op under the
            // Epic 5 JSON contract - a single top-level JSON object can't be meaningfully parsed until
            // it's fully closed, so the live file-reveal T6.2.2 added only still fires for a model that
            // (incorrectly) reverts to the old markdown shape. The authoritative parse happens once,
            // below, via StructuredResponseParser against the complete output.
            void OnTextDelta(string text) => Dispatcher.UIThread.Post(() =>
            {
                CliOutput += text;

                var streamedSoFar = ClaudeResponseParser.Parse(CliOutput);
                _promptResultStore.SetResult(streamedSoFar.Tutorial, streamedSoFar.Files, WorkingDirectoryPath);
            });

            var result = await _llmRunner.RunPromptAsync(fullPrompt, WorkingDirectoryPath, OnTextDelta, linkedCancellationTokenSource.Token);
            StructuredParseResult? parseResult = null;

            if (result.Success)
            {
                parseResult = StructuredResponseParser.Parse(result.Output, WorkingDirectoryPath);

                // T5.1.2: one retry, feeding the schema validation error back to the model, before
                // settling for the markdown fallback (T5.1.3). A failed retry call (cancelled, CLI
                // error) keeps the original attempt's already-fallback-parsed result rather than
                // discarding it for an error state.
                if (parseResult.UsedFallback && parseResult.ValidationError is not null)
                {
                    var retryPrompt = fullPrompt + string.Format(RetryInstructionSuffix, parseResult.ValidationError);
                    var retryResult = await _llmRunner.RunPromptAsync(retryPrompt, WorkingDirectoryPath, OnTextDelta, linkedCancellationTokenSource.Token);

                    if (retryResult.Success)
                    {
                        result = CombineUsage(result, retryResult);
                        parseResult = StructuredResponseParser.Parse(retryResult.Output, WorkingDirectoryPath);
                    }
                }
            }

            CliOutput = result.Success || string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.Output
                : result.ErrorMessage;

            if (result.TotalCostUsd.HasValue)
            {
                UsageSummary = string.Format(
                    "Prompt_UsageSummary".GetLocalized(),
                    result.TotalCostUsd.Value,
                    result.InputTokens ?? 0,
                    result.OutputTokens ?? 0,
                    (result.DurationMs ?? 0) / 1000.0);
            }

            if (result.Success)
            {
                var parsed = parseResult!.Response;
                _promptResultStore.SetResult(parsed.Tutorial, parsed.Files, WorkingDirectoryPath);

                var completionMessage = parsed.Files.Count > 0
                    ? string.Format("Prompt_RunCompletedWithFiles".GetLocalized(), parsed.Files.Count)
                    : "Prompt_RunCompleted".GetLocalized();

                StatusMessage = parseResult.UsedFallback
                    ? completionMessage + " " + "Prompt_FallbackParseNotice".GetLocalized()
                    : completionMessage;
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

    // T5.1.2's retry is a second full LLM call, so cost/token/duration reporting (T6.2.3) has to add
    // both calls together rather than silently reporting only the retry's - otherwise a schema-failure
    // retry would under-report what the run actually spent.
    private static LlmRunResult CombineUsage(LlmRunResult original, LlmRunResult retry) => new()
    {
        Success = retry.Success,
        Output = retry.Output,
        ErrorMessage = retry.ErrorMessage,
        ExitCode = retry.ExitCode,
        TotalCostUsd = SumNullable(original.TotalCostUsd, retry.TotalCostUsd),
        DurationMs = SumNullable(original.DurationMs, retry.DurationMs),
        InputTokens = SumNullable(original.InputTokens, retry.InputTokens),
        OutputTokens = SumNullable(original.OutputTokens, retry.OutputTokens),
    };

    private static double? SumNullable(double? a, double? b) => a.HasValue || b.HasValue ? (a ?? 0) + (b ?? 0) : null;

    private static long? SumNullable(long? a, long? b) => a.HasValue || b.HasValue ? (a ?? 0) + (b ?? 0) : null;

    private static int? SumNullable(int? a, int? b) => a.HasValue || b.HasValue ? (a ?? 0) + (b ?? 0) : null;

    private bool CanRunPrompt() => !IsRunning && !string.IsNullOrWhiteSpace(PromptText);

    private bool CanCancelPrompt() => _runCancellationTokenSource is not null;

    partial void OnPromptTextChanged(string value) => RunPromptCommand.NotifyCanExecuteChanged();

    partial void OnIsRunningChanged(bool value) => RunPromptCommand.NotifyCanExecuteChanged();

    partial void OnTimeoutSecondsChanged(int value) => _localSettingsService.SaveSettingAsync(TimeoutSecondsSettingsKey, value);
}
