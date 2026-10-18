using Paarde.Core.Contracts.Services;
using Paarde.Core.Models;

namespace Paarde.Avalonia.Tests.Fakes;

public class FakeRunSessionStore : IRunSessionStore
{
    public int RecordRunCallCount { get; private set; }

    public string? LastRecordedPrompt { get; private set; }

    public string? LastRecordedRawOutput { get; private set; }

    public int ResaveCallCount { get; private set; }

    public int RestoreLastSessionCallCount { get; private set; }

    // Null by default (no prior run to restore) - tests that need a restorable session set this.
    public RunSession? SessionToRestore { get; set; }

    public void RecordRun(string prompt, string rawOutput)
    {
        RecordRunCallCount++;
        LastRecordedPrompt = prompt;
        LastRecordedRawOutput = rawOutput;
    }

    public void Resave() => ResaveCallCount++;

    public RunSession? RestoreLastSession()
    {
        RestoreLastSessionCallCount++;
        return SessionToRestore;
    }
}
