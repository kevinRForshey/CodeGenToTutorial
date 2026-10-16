using CodeGenToTutorial.Core.Models;
using CodeGenToTutorial.Core.ViewModels;

using Newtonsoft.Json;

namespace CodeGenToTutorial.Core.Helpers;

// Parses the JSON response contract requested by PromptViewModel.TutorialInstructionPrompt (Epic 5 /
// F5.1) - a single JSON object, either wrapped in commentary/fences or raw, matching ClaudeJsonResponse.
// TryParse never throws: any extraction/deserialization/validation failure comes back as a false
// return plus a human-readable `error`, which PromptViewModel feeds back to the model on a one-shot
// retry (T5.1.2) before giving up and handing the raw text to the legacy markdown parser (T5.1.3).
public static class ClaudeJsonResponseParser
{
    public static bool TryParse(string cliOutput, out ClaudeParsedResponse? response, out string? error)
    {
        response = null;

        var jsonText = ExtractJsonObject(cliOutput);

        if (jsonText is null)
        {
            error = "No JSON object was found in the response.";
            return false;
        }

        ClaudeJsonResponse? parsed;

        try
        {
            parsed = JsonConvert.DeserializeObject<ClaudeJsonResponse>(jsonText);
        }
        catch (JsonException ex)
        {
            error = $"The response was not valid JSON: {ex.Message}";
            return false;
        }

        // Defensive: JsonConvert.DeserializeObject<T> only returns null for the literal "null", which
        // ExtractJsonObject can never produce (it always returns a balanced `{...}` substring) -
        // unreachable in practice, left untested rather than forcing a contrived repro.
        if (parsed is null)
        {
            error = "The response's JSON object could not be deserialized.";
            return false;
        }

        if (!TryValidate(parsed, out error))
        {
            return false;
        }

        response = Map(parsed);
        return true;
    }

    private static bool TryValidate(ClaudeJsonResponse response, out string? error)
    {
        var responseType = response.ResponseType?.Trim().ToLowerInvariant();

        if (responseType != "answer" && responseType != "development")
        {
            error = "\"responseType\" must be either \"answer\" or \"development\".";
            return false;
        }

        if (responseType == "answer")
        {
            if (string.IsNullOrWhiteSpace(response.Answer))
            {
                error = "\"answer\" is required and must be non-empty when \"responseType\" is \"answer\".";
                return false;
            }

            error = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(response.Tutorial))
        {
            error = "\"tutorial\" is required and must be non-empty when \"responseType\" is \"development\".";
            return false;
        }

        foreach (var file in response.Files ?? [])
        {
            if (string.IsNullOrWhiteSpace(file.Path))
            {
                error = "Every entry in \"files\" must have a non-empty \"path\".";
                return false;
            }

            if (file.Content is null)
            {
                error = $"File \"{file.Path}\" is missing \"content\".";
                return false;
            }

            foreach (var step in file.Steps ?? [])
            {
                if (string.IsNullOrWhiteSpace(step.Title))
                {
                    error = $"A step in file \"{file.Path}\" is missing a non-empty \"title\".";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }

    private static ClaudeParsedResponse Map(ClaudeJsonResponse response)
    {
        if (response.ResponseType!.Trim().Equals("answer", StringComparison.OrdinalIgnoreCase))
        {
            return new ClaudeParsedResponse(response.Answer!.Trim(), []);
        }

        var files = (response.Files ?? [])
            .Select(file =>
            {
                var content = file.Content ?? string.Empty;

                return new ProposedFileChangeViewModel(file.Path!.Trim(), content)
                {
                    Hunks = (file.Steps ?? [])
                        .Select(step =>
                        {
                            var snippet = step.Snippet ?? string.Empty;

                            return new ProposedHunk(
                                step.Title!.Trim(),
                                step.Explanation?.Trim() ?? string.Empty,
                                snippet,
                                HunkLocationResolver.Resolve(content, snippet));
                        })
                        .ToList(),
                };
            })
            .ToList();

        return new ClaudeParsedResponse(response.Tutorial!.Trim(), files);
    }

    // Scans for the first top-level `{ ... }` object in the text, skipping over braces that appear
    // inside JSON string values (e.g. a file's "content" is full of literal code with its own braces)
    // so it isn't fooled into closing early. Tolerates surrounding commentary or markdown fences,
    // since a model asked for "JSON only" will still sometimes wrap it in prose or a ```json block.
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');

        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escapeNext = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escapeNext)
                {
                    escapeNext = false;
                }
                else if (c == '\\')
                {
                    escapeNext = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return text[start..(i + 1)];
                }
            }
        }

        return null;
    }
}
