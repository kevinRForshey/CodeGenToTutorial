using System.Text.RegularExpressions;

using CodeGenToTutorial.Core.Models;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Helpers;

// Parses the "## Tutorial" / "## Files" structured response requested by PromptViewModel.TutorialInstructionPrompt.
// Each file entry may nest its own "#### Tutorial" section made up of "##### Step:" blocks that explain the
// reasoning behind that part of the file's change, paired with the specific code it introduces.
public static partial class ClaudeResponseParser
{
    private const string TutorialHeader = "## Tutorial";
    private const string FilesHeader = "## Files";

    public static ClaudeParsedResponse Parse(string cliOutput)
    {
        if (string.IsNullOrWhiteSpace(cliOutput))
        {
            return new ClaudeParsedResponse(string.Empty, Array.Empty<ProposedFileChangeViewModel>());
        }

        var tutorialIndex = cliOutput.IndexOf(TutorialHeader, StringComparison.OrdinalIgnoreCase);
        var filesIndex = cliOutput.IndexOf(FilesHeader, StringComparison.OrdinalIgnoreCase);

        if (tutorialIndex < 0 || filesIndex < 0 || filesIndex < tutorialIndex)
        {
            // Not a structured development response - e.g. a plain answer to a general question.
            return new ClaudeParsedResponse(cliOutput.Trim(), Array.Empty<ProposedFileChangeViewModel>());
        }

        var tutorial = cliOutput[(tutorialIndex + TutorialHeader.Length)..filesIndex].Trim();
        var filesSection = cliOutput[(filesIndex + FilesHeader.Length)..];

        var files = FileEntryRegex().Matches(filesSection)
            .Select(match =>
            {
                var content = match.Groups["content"].Value;

                return new ProposedFileChangeViewModel(match.Groups["path"].Value.Trim(), content)
                {
                    Hunks = ParseHunks(match.Groups["tail"].Value, content),
                };
            })
            .Where(file => file.FilePath.Length > 0)
            .ToList();

        return new ClaudeParsedResponse(tutorial, files);
    }

    private static IReadOnlyList<ProposedHunk> ParseHunks(string tail, string fileContent) =>
        StepEntryRegex().Matches(tail)
            .Select(match => new TutorialStep(
                match.Groups["title"].Value.Trim(),
                match.Groups["explanation"].Value.Trim(),
                match.Groups["code"].Value))
            .Where(step => step.Title.Length > 0)
            .Select(step => new ProposedHunk(step.Title, step.Explanation, step.Code, HunkLocationResolver.Resolve(fileContent, step.Code)))
            .ToList();

    [GeneratedRegex(@"^###\s*File:\s*(?<path>.+?)\s*\r?\n```[^\r\n]*\r?\n(?<content>.*?)\r?\n```(?<tail>.*?)(?=^###\s*File:|\z)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FileEntryRegex();

    [GeneratedRegex(@"^#{4,5}\s*Step:\s*(?<title>.+?)\s*\r?\n(?<explanation>.*?)```[^\r\n]*\r?\n(?<code>.*?)\r?\n```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex StepEntryRegex();
}
