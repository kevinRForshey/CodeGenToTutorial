using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

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
    private const string PresetSettingsKey = "SavedPromptPreset";
    private const string DepthSettingsKey = "SavedTutorialDepth";
    private const string ContextPathsSettingsKey = "SavedContextPaths";
    private const int DefaultTimeoutSeconds = 300;

    // T5.3.1: the instructions prepended to every prompt sent to Claude Code used to live here as a
    // hard-coded const; now it's CodeGenToTutorial.Core/Prompts/TutorialInstructionPromptV1.txt,
    // loaded through IPromptTemplateProvider so the schema contract can change (a new versioned
    // .txt file) without touching this ViewModel, and so T5.3.2 can later let a user override it
    // from Settings behind the same interface. The response contract is a single JSON object
    // (CodeGenToTutorial.Core.Models.ClaudeJsonResponse) instead of markdown headers, parsed by
    // CodeGenToTutorial.Core.Helpers.ClaudeJsonResponseParser; the old markdown parser
    // (ClaudeResponseParser) is kept only as a last-resort fallback (T5.1.3) via
    // StructuredResponseParser.
    private const string RetryInstructionSuffix =
        "\n\nYour previous response could not be parsed: {0}\n\nRespond again with ONLY a single corrected JSON object matching the schema above - no markdown fences, no commentary.";

    private readonly ILlmRunner _llmRunner;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFilePickerService _filePickerService;
    private readonly IPromptResultStore _promptResultStore;
    private readonly IPromptTemplateProvider _promptTemplateProvider;

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

    // T5.3.3: named presets/depth controls that append extra guidance onto the instruction
    // preamble (default or user-overridden, T5.3.2) ahead of the user's own prompt text.
    [ObservableProperty]
    private PromptPreset selectedPreset = PromptPreset.General;

    [ObservableProperty]
    private TutorialDepth selectedDepth = TutorialDepth.Standard;

    public IReadOnlyList<PromptPreset> AvailablePresets { get; } = Enum.GetValues<PromptPreset>();

    public IReadOnlyList<TutorialDepth> AvailableDepths { get; } = Enum.GetValues<TutorialDepth>();

    // T5.4.1: files/folders the user attaches as extra context. A folder is read non-recursively -
    // only the files directly inside it - so attaching one can't silently sweep in .git/bin/obj/
    // node_modules; the user can attach a subfolder explicitly if they want more. Unreadable/binary
    // files are skipped when building the prompt (see BuildContextFilesBlock) rather than failing
    // the whole run.
    public ObservableCollection<string> AttachedPaths { get; } = new();

    [ObservableProperty]
    private string? selectedAttachedPath;

    // T6.2.3: live "Xs" readout ticking while a run is in progress, and a post-run cost/token
    // summary once the CLI's final result line has been parsed.
    [ObservableProperty]
    private string elapsedTimeDisplay = string.Empty;

    [ObservableProperty]
    private string usageSummary = string.Empty;

    // T5.4.2: a live "~N tokens" estimate of the exact prompt BuildFullPromptAsync would send,
    // recomputed whenever something that feeds into it changes (text, working directory, preset/
    // depth, attachments) - so the user has a size sense before clicking Send, not just after.
    [ObservableProperty]
    private string estimatedTokenCountDisplay = string.Empty;

    // Guards against an older recompute's settings read finishing after a newer one and
    // overwriting EstimatedTokenCountDisplay with stale data - a real risk here (unlike most other
    // settings reads in this class) since typing triggers a recompute on every keystroke.
    private int _tokenEstimateVersion;

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

    public PromptViewModel(ILlmRunner llmRunner, ILocalSettingsService localSettingsService, IFolderPickerService folderPickerService, IFilePickerService filePickerService, IPromptResultStore promptResultStore, IPromptTemplateProvider promptTemplateProvider)
    {
        _llmRunner = llmRunner;
        _localSettingsService = localSettingsService;
        _folderPickerService = folderPickerService;
        _filePickerService = filePickerService;
        _promptResultStore = promptResultStore;
        _promptTemplateProvider = promptTemplateProvider;

        // T5.4.2: ObservableCollection<T> doesn't go through an [ObservableProperty]'s On*Changed
        // hook, so attachments need their own recompute trigger here instead.
        AttachedPaths.CollectionChanged += (_, _) => UpdateEstimatedTokenCountDisplay();
    }

    public async void OnNavigatedTo(object parameter)
    {
        PromptText = await _localSettingsService.ReadSettingAsync<string>(PromptSettingsKey) ?? string.Empty;
        WorkingDirectoryPath = await _localSettingsService.ReadSettingAsync<string>(WorkingDirectorySettingsKey) ?? string.Empty;

        var savedTimeoutSeconds = await _localSettingsService.ReadSettingAsync<int>(TimeoutSecondsSettingsKey);
        TimeoutSeconds = savedTimeoutSeconds > 0 ? savedTimeoutSeconds : DefaultTimeoutSeconds;

        var savedPreset = await _localSettingsService.ReadSettingAsync<string>(PresetSettingsKey);
        SelectedPreset = Enum.TryParse<PromptPreset>(savedPreset, out var preset) ? preset : PromptPreset.General;

        var savedDepth = await _localSettingsService.ReadSettingAsync<string>(DepthSettingsKey);
        SelectedDepth = Enum.TryParse<TutorialDepth>(savedDepth, out var depth) ? depth : TutorialDepth.Standard;

        var savedContextPaths = await _localSettingsService.ReadSettingAsync<List<string>>(ContextPathsSettingsKey);
        AttachedPaths.Clear();
        foreach (var path in savedContextPaths ?? [])
        {
            AttachedPaths.Add(path);
        }

        StatusMessage = string.Empty;
        UpdateEstimatedTokenCountDisplay();
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

    [RelayCommand]
    private async Task AddContextFilesAsync()
    {
        var files = await _filePickerService.PickFilesAsync();
        AddAttachedPaths(files);
        await SaveAttachedPathsAsync();
    }

    [RelayCommand]
    private async Task AddContextFolderAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync();

        if (folder != null)
        {
            AddAttachedPaths([folder]);
            await SaveAttachedPathsAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveAttachedPath))]
    private async Task RemoveAttachedPathAsync()
    {
        if (SelectedAttachedPath is null)
        {
            return;
        }

        AttachedPaths.Remove(SelectedAttachedPath);
        SelectedAttachedPath = null;
        await SaveAttachedPathsAsync();
    }

    private bool CanRemoveAttachedPath() => SelectedAttachedPath is not null;

    private void AddAttachedPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!AttachedPaths.Contains(path))
            {
                AttachedPaths.Add(path);
            }
        }
    }

    private Task SaveAttachedPathsAsync() => _localSettingsService.SaveSettingAsync(ContextPathsSettingsKey, AttachedPaths.ToList());

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
            var fullPrompt = await BuildFullPromptAsync();

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

    // T5.4.2: the exact string that would be sent to the LLM - shared by RunPromptAsync and the
    // live token estimate (UpdateEstimatedTokenCount) so the estimate can't drift from what a run
    // actually sends.
    private async Task<string> BuildFullPromptAsync()
    {
        // T5.3.2: read fresh on every call (not cached on OnNavigatedTo) so a change made on the
        // Settings page takes effect immediately - same rationale as ClaudeCliOptionsProvider
        // reading its settings fresh on every call.
        var instructionTemplateOverride = await _localSettingsService.ReadSettingAsync<string>(PromptTemplateSettingsKeys.Override);
        var instructionTemplate = string.IsNullOrWhiteSpace(instructionTemplateOverride)
            ? _promptTemplateProvider.GetTutorialInstructionPrompt()
            : instructionTemplateOverride;

        // T5.3.3: preset/depth guidance is additive to whatever the effective preamble is (default
        // or user-overridden) - it's extra instruction text, not schema, so it applies the same way
        // regardless of where the preamble came from.
        var presetInstruction = _promptTemplateProvider.GetPresetInstruction(SelectedPreset);
        var depthInstruction = _promptTemplateProvider.GetDepthInstruction(SelectedDepth);
        var extraInstructions = string.Concat(
            string.IsNullOrEmpty(presetInstruction) ? string.Empty : "\n\n" + presetInstruction,
            string.IsNullOrEmpty(depthInstruction) ? string.Empty : "\n\n" + depthInstruction);

        // T5.4.1: user-attached files/folders, embedded ahead of the user's own prompt text so the
        // model has their content before reading what to do with it.
        var contextFilesBlock = BuildContextFilesBlock(AttachedPaths);

        var fullPrompt = instructionTemplate + extraInstructions + contextFilesBlock + "\n\n" + PromptText;

        if (!string.IsNullOrWhiteSpace(WorkingDirectoryPath))
        {
            fullPrompt += $"\n\nThe working directory is: {WorkingDirectoryPath}";
        }

        return fullPrompt;
    }

    // T5.4.1: a folder attachment is expanded to the files directly inside it (non-recursive, see
    // AttachedPaths' own comment); a file attachment is used as-is. A file that can't be read as
    // text (binary, permissions, deleted since being attached) is skipped rather than failing the
    // whole run - folder attachment in particular can easily pick up a non-text file alongside the
    // ones that matter. Returns "" (no header) if nothing could be read.
    private static string BuildContextFilesBlock(IEnumerable<string> attachedPaths)
    {
        var filePaths = attachedPaths.SelectMany(path => Directory.Exists(path)
            ? Directory.EnumerateFiles(path)
            : File.Exists(path) ? [path] : Enumerable.Empty<string>());

        var block = new StringBuilder();

        foreach (var filePath in filePaths)
        {
            string content;

            try
            {
                content = File.ReadAllText(filePath);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            block.Append($"\n\n--- {filePath} ---\n{content}");
        }

        return block.Length > 0 ? "\n\nAdditional context files:" + block : string.Empty;
    }

    private static double? SumNullable(double? a, double? b) => a.HasValue || b.HasValue ? (a ?? 0) + (b ?? 0) : null;

    private static long? SumNullable(long? a, long? b) => a.HasValue || b.HasValue ? (a ?? 0) + (b ?? 0) : null;

    private static int? SumNullable(int? a, int? b) => a.HasValue || b.HasValue ? (a ?? 0) + (b ?? 0) : null;

    private bool CanRunPrompt() => !IsRunning && !string.IsNullOrWhiteSpace(PromptText);

    private bool CanCancelPrompt() => _runCancellationTokenSource is not null;

    partial void OnPromptTextChanged(string value)
    {
        RunPromptCommand.NotifyCanExecuteChanged();
        UpdateEstimatedTokenCountDisplay();
    }

    partial void OnIsRunningChanged(bool value) => RunPromptCommand.NotifyCanExecuteChanged();

    partial void OnTimeoutSecondsChanged(int value) => _localSettingsService.SaveSettingAsync(TimeoutSecondsSettingsKey, value);

    partial void OnWorkingDirectoryPathChanged(string value) => UpdateEstimatedTokenCountDisplay();

    partial void OnSelectedPresetChanged(PromptPreset value)
    {
        _localSettingsService.SaveSettingAsync(PresetSettingsKey, value.ToString());
        UpdateEstimatedTokenCountDisplay();
    }

    partial void OnSelectedDepthChanged(TutorialDepth value)
    {
        _localSettingsService.SaveSettingAsync(DepthSettingsKey, value.ToString());
        UpdateEstimatedTokenCountDisplay();
    }

    partial void OnSelectedAttachedPathChanged(string? value) => RemoveAttachedPathCommand.NotifyCanExecuteChanged();

    private async void UpdateEstimatedTokenCountDisplay()
    {
        var version = ++_tokenEstimateVersion;
        var fullPrompt = await BuildFullPromptAsync();

        if (version != _tokenEstimateVersion)
        {
            // A newer change (another keystroke, another attachment) started its own recompute
            // after this one - let that one's result win instead of overwriting it with ours.
            return;
        }

        EstimatedTokenCountDisplay = string.Format(
            "Prompt_EstimatedTokenCount".GetLocalized(), TokenEstimator.EstimateTokenCount(fullPrompt));
    }
}
