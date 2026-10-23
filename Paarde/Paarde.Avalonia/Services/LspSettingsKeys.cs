namespace Paarde.Avalonia.Services;

// Shared between SettingsViewModel (where these are edited) and LspOptionsProvider (where they're
// read to build the LspOptions passed into StdioLanguageServerClient) - same split as
// ClaudeCliSettingsKeys/ClaudeCliOptionsProvider (T6.1.4).
public static class LspSettingsKeys
{
    public const string ExecutablePath = "LspExecutablePath";

    public const string Arguments = "LspArguments";
}
