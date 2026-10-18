using Paarde.Core.Contracts.Services;

namespace Paarde.Avalonia.Tests.Fakes;

public class FakeRunSessionStore : IRunSessionStore
{
    public int RecordRunCallCount { get; private set; }

    public string? LastRecordedPrompt { get; private set; }

    public string? LastRecordedRawOutput { get; private set; }

    public int ResaveCallCount { get; private set; }

    public void RecordRun(string prompt, string rawOutput)
    {
        RecordRunCallCount++;
        LastRecordedPrompt = prompt;
        LastRecordedRawOutput = rawOutput;
    }

    public void Resave() => ResaveCallCount++;
}
