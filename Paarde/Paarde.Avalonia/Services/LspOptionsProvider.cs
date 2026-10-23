using Paarde.Avalonia.Contracts.Services;
using Paarde.Core.Models;

namespace Paarde.Avalonia.Services;

// Same seam as ClaudeCliOptionsProvider: reads Settings fresh on every call rather than caching,
// so a change made there takes effect on the next StartAsync without needing an app restart.
public class LspOptionsProvider
{
    private readonly ILocalSettingsService _localSettingsService;

    public LspOptionsProvider(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task<LspOptions> GetOptionsAsync()
    {
        var executablePath = await _localSettingsService.ReadSettingAsync<string>(LspSettingsKeys.ExecutablePath);
        var arguments = await _localSettingsService.ReadSettingAsync<string>(LspSettingsKeys.Arguments);

        return new LspOptions(
            string.IsNullOrWhiteSpace(executablePath) ? LspOptions.Default.ExecutablePath : executablePath,
            string.IsNullOrWhiteSpace(arguments) ? LspOptions.Default.Arguments : arguments);
    }
}
