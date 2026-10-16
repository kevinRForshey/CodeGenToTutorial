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

    // F5.2 / T5.2.1: a cheaper alternative to "content" for files that already exist - each entry is
    // a search/replace block instead of the whole file, applied locally against the file's current
    // on-disk content by SearchReplaceEditApplier. Takes priority over "content" when non-empty.
    public List<ClaudeJsonEdit>? Edits { get; set; }
}

public class ClaudeJsonStep
{
    public string? Title { get; set; }

    public string? Explanation { get; set; }

    public string? Snippet { get; set; }
}

// F5.2: one search/replace edit block within a file's "edits" list.
public class ClaudeJsonEdit
{
    public string? Title { get; set; }

    public string? Explanation { get; set; }

    public string? Search { get; set; }

    public string? Replace { get; set; }
}
