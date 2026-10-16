namespace CodeGenToTutorial.Core.Models;

// Wire format requested by PromptViewModel's JSON-based TutorialInstructionPrompt (Epic 5 / F5.1),
// replacing the "## Tutorial" / "## Files" markdown headers ClaudeResponseParser scrapes. All
// properties are nullable so a malformed/incomplete response deserializes instead of throwing,
// letting ClaudeJsonResponseParser produce a precise validation error (fed back on retry) rather
// than a generic deserialization exception.
public class ClaudeJsonResponse
{
    public string? ResponseType { get; set; }

    public string? Answer { get; set; }

    public string? Tutorial { get; set; }

    public List<ClaudeJsonFile>? Files { get; set; }
}

public class ClaudeJsonFile
{
    public string? Path { get; set; }

    public string? Content { get; set; }

    public List<ClaudeJsonStep>? Steps { get; set; }
}

public class ClaudeJsonStep
{
    public string? Title { get; set; }

    public string? Explanation { get; set; }

    public string? Snippet { get; set; }
}
