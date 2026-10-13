using CodeGenToTutorial.Avalonia.Contracts.Services;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakeDialogService : IDialogService
{
    public bool ConfirmResultToReturn { get; set; } = true;

    public string? LastConfirmTitle { get; private set; }

    public string? LastConfirmMessage { get; private set; }

    public string? LastInfoTitle { get; private set; }

    public string? LastInfoMessage { get; private set; }

    public int ConfirmCallCount { get; private set; }

    public int InfoCallCount { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message, string confirmButtonText, string cancelButtonText)
    {
        ConfirmCallCount++;
        LastConfirmTitle = title;
        LastConfirmMessage = message;
        return Task.FromResult(ConfirmResultToReturn);
    }

    public Task InfoAsync(string title, string message, string closeButtonText)
    {
        InfoCallCount++;
        LastInfoTitle = title;
        LastInfoMessage = message;
        return Task.CompletedTask;
    }
}
