namespace CodeGenToTutorial.Avalonia.Strings;

// Stand-in for the WinUI project's .resw + x:Uid localization pipeline, which relies on Visual Studio/UWP
// designer tooling not available for a cross-platform Avalonia build. Keys mirror the original Resources.resw
// entries that are looked up from code (ViewModels/code-behind); markup-only strings (button labels, headers)
// are set directly in .axaml instead, since x:Uid has no Avalonia equivalent.
public static class Resources
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["AppDisplayName"] = "CodeGenToTutorial",
        ["Prompt_Saved"] = "Prompt saved.",
        ["Prompt_Running"] = "Running Claude Code...",
        ["Prompt_RunCompleted"] = "Claude Code finished.",
        ["Prompt_RunCompletedWithFiles"] = "Claude Code finished. {0} file(s) ready to review on the Diffs page.",
        ["Prompt_RunFailed"] = "Claude Code exited with code {0}.",
        ["Prompt_RunError"] = "Failed to run Claude Code: {0}",
        ["Tutorial_ChangeApplied"] = "Applied the changes to {0}.",
        ["Tutorial_ApplyFailed"] = "Failed to apply the change: {0}",
        ["Tutorial_ApplySelectedNoneSelected"] = "No changes are selected to apply.",
        ["Tutorial_ApplySelectedConfirmTitle"] = "Apply selected changes?",
        ["Tutorial_ApplySelectedConfirmMessage"] = "This will write {0} change(s) across {1} file(s) to disk.",
        ["Tutorial_ApplySelectedConfirmButton"] = "Apply",
        ["Tutorial_ApplySelectedCancelButton"] = "Cancel",
        ["Tutorial_ApplySelectedResultTitle"] = "Apply results",
        ["Tutorial_ApplySelectedResultClose"] = "OK",
        ["Tutorial_ApplySelectedResultApplied"] = "Applied {0} file(s).",
        ["Tutorial_ApplySelectedResultConflicts"] = "{0} file(s) changed on disk since this proposal and were skipped: {1}. Open the Diffs page and select one to re-diff it against the current file, then try applying again.",
        ["Tutorial_ApplySelectedResultFailures"] = "{0} file(s) failed to apply: {1}.",
    };

    public static string GetLocalized(this string resourceKey) =>
        Map.TryGetValue(resourceKey, out var value) ? value : resourceKey;
}
