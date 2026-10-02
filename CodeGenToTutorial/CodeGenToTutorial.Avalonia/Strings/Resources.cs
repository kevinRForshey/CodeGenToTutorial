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
    };

    public static string GetLocalized(this string resourceKey) =>
        Map.TryGetValue(resourceKey, out var value) ? value : resourceKey;
}
