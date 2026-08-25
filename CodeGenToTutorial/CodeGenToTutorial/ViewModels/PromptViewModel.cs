using CodeGenToTutorial.Contracts.Services;
using CodeGenToTutorial.Contracts.ViewModels;
using CodeGenToTutorial.Helpers;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.ViewModels;

public partial class PromptViewModel : ObservableRecipient, INavigationAware
{
    private const string PromptSettingsKey = "SavedPrompt";
    private const string WorkingDirectorySettingsKey = "SavedWorkingDirectory";

    // Hard-coded instructions prepended to every prompt sent to Claude Code. Refine as the tutorial/diff workflow evolves.
    private const string TutorialInstructionPrompt =
        """
        You are being called from a desktop app. First decide what kind of request the user made below:

        1. General question (math, trivia, "explain X", anything that does not require changing files in this
           codebase): just answer it directly and concisely. Do not produce a tutorial or file list for these.

        2. Development request (asks for a feature, fix, or change to this codebase): do NOT modify any files.
           Instead respond with exactly two sections, using these headers so they can be parsed:

           ## Tutorial
           A step-by-step explanation of how the user could implement the change themselves.

           ## Files
           For every file that needs to change, add a subsection formatted exactly like this, repeated for each
           file:

           ### File: <path to the file, relative to the working directory>
           ```
           <the complete contents of the file with the proposed changes applied - the whole file, not a diff>
           ```

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
        IsRunning = true;
        CliOutput = string.Empty;
        StatusMessage = "Prompt_Running".GetLocalized();

        try
        {
            var fullPrompt = TutorialInstructionPrompt + PromptText;

            if (!string.IsNullOrWhiteSpace(WorkingDirectoryPath))
            {
                fullPrompt += $"\n\nThe working directory is: {WorkingDirectoryPath}";
            }

            var result = await _claudeCliService.RunPromptAsync(fullPrompt, WorkingDirectoryPath);

            CliOutput = result.ExitCode == 0 || string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

            if (result.ExitCode == 0)
            {
                var parsed = ClaudeResponseParser.Parse(CliOutput);
                _promptResultStore.SetResult(parsed.Tutorial, parsed.Files);

                StatusMessage = parsed.Files.Count > 0
                    ? string.Format("Prompt_RunCompletedWithFiles".GetLocalized(), parsed.Files.Count)
                    : "Prompt_RunCompleted".GetLocalized();
            }
            else
            {
                StatusMessage = string.Format("Prompt_RunFailed".GetLocalized(), result.ExitCode);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("Prompt_RunError".GetLocalized(), ex.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanRunPrompt() => !IsRunning && !string.IsNullOrWhiteSpace(PromptText);

    partial void OnPromptTextChanged(string value) => RunPromptCommand.NotifyCanExecuteChanged();

    partial void OnIsRunningChanged(bool value) => RunPromptCommand.NotifyCanExecuteChanged();
}
