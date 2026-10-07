using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Helpers;

// Locates a tutorial step's snippet (only the new/changed lines for that step) within the file's full
// proposed content, so a ProposedHunk knows which lines it actually produced instead of just carrying
// the snippet text for display. First exact line-sequence match wins; if the snippet doesn't appear
// verbatim (model reformatted it, or it's non-contiguous) the location is left unresolved.
public static class HunkLocationResolver
{
    public static HunkLocation? Resolve(string fileContent, string snippet)
    {
        var fileLines = SplitLines(fileContent);
        var snippetLines = SplitLines(snippet);

        if (snippetLines.Length == 0 || snippetLines.Length > fileLines.Length)
        {
            return null;
        }

        for (var start = 0; start <= fileLines.Length - snippetLines.Length; start++)
        {
            var isMatch = true;

            for (var offset = 0; offset < snippetLines.Length; offset++)
            {
                if (fileLines[start + offset] != snippetLines[offset])
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
            {
                return new HunkLocation(start, start + snippetLines.Length - 1);
            }
        }

        return null;
    }

    private static string[] SplitLines(string text) =>
        string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Replace("\r\n", "\n").Split('\n');
}
