using System.Text.RegularExpressions;

using CodeGenToTutorial.Models;

namespace CodeGenToTutorial.Helpers;

// Parses the "## Tutorial" / "## Files" structured response requested by PromptViewModel.TutorialInstructionPrompt.
public static partial class ClaudeResponseParser
{
    private const string TutorialHeader = "## Tutorial";
    private const string FilesHeader = "## Files";

    public static ClaudeParsedResponse Parse(string cliOutput)
    {
        if (string.IsNullOrWhiteSpace(cliOutput))
        {
            return new ClaudeParsedResponse(string.Empty, Array.Empty<ProposedFileChange>());
        }

        var tutorialIndex = cliOutput.IndexOf(TutorialHeader, StringComparison.OrdinalIgnoreCase);
        var filesIndex = cliOutput.IndexOf(FilesHeader, StringComparison.OrdinalIgnoreCase);

        if (tutorialIndex < 0 || filesIndex < 0 || filesIndex < tutorialIndex)
        {
            // Not a structured development response - e.g. a plain answer to a general question.
            return new ClaudeParsedResponse(cliOutput.Trim(), Array.Empty<ProposedFileChange>());
        }

        var tutorial = cliOutput[(tutorialIndex + TutorialHeader.Length)..filesIndex].Trim();
        var filesSection = cliOutput[(filesIndex + FilesHeader.Length)..];

        var files = FileEntryRegex().Matches(filesSection)
            .Select(match => new ProposedFileChange
            {
                FilePath = match.Groups["path"].Value.Trim(),
                Content = match.Groups["content"].Value,
            })
            .Where(file => file.FilePath.Length > 0)
            .ToList();

        return new ClaudeParsedResponse(tutorial, files);
    }

    [GeneratedRegex(@"^###\s*File:\s*(?<path>.+?)\s*\r?\n```[^\r\n]*\r?\n(?<content>.*?)\r?\n```\s*$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FileEntryRegex();
}
