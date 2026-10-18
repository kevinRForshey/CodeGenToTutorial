using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Services;

public class PromptTemplateProvider : IPromptTemplateProvider
{
    public const int LatestVersion = 1;

    private const string ResourceName = "CodeGenToTutorial.Core.Prompts.TutorialInstructionPromptV1.txt";

    private readonly Dictionary<string, string> _cache = [];

    public int CurrentVersion => LatestVersion;

    public string GetTutorialInstructionPrompt() => GetCached(ResourceName);

    public string GetPresetInstruction(PromptPreset preset) => preset switch
    {
        PromptPreset.General => string.Empty,
        PromptPreset.Feature => GetCached("CodeGenToTutorial.Core.Prompts.PresetFeatureV1.txt"),
        PromptPreset.Bugfix => GetCached("CodeGenToTutorial.Core.Prompts.PresetBugfixV1.txt"),
        PromptPreset.Refactor => GetCached("CodeGenToTutorial.Core.Prompts.PresetRefactorV1.txt"),
        PromptPreset.Explain => GetCached("CodeGenToTutorial.Core.Prompts.PresetExplainV1.txt"),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };

    public string GetDepthInstruction(TutorialDepth depth) => depth switch
    {
        TutorialDepth.Standard => string.Empty,
        TutorialDepth.Brief => GetCached("CodeGenToTutorial.Core.Prompts.DepthBriefV1.txt"),
        TutorialDepth.Detailed => GetCached("CodeGenToTutorial.Core.Prompts.DepthDetailedV1.txt"),
        _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, null),
    };

    private string GetCached(string resourceName)
    {
        if (!_cache.TryGetValue(resourceName, out var content))
        {
            content = ReadEmbeddedResource(resourceName);
            _cache[resourceName] = content;
        }

        return content;
    }

    private static string ReadEmbeddedResource(string resourceName)
    {
        var assembly = typeof(PromptTemplateProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded prompt template resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
