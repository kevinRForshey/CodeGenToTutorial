using CodeGenToTutorial.Core.Contracts.Services;

namespace CodeGenToTutorial.Core.Services;

public class PromptTemplateProvider : IPromptTemplateProvider
{
    public const int LatestVersion = 1;

    private const string ResourceName = "CodeGenToTutorial.Core.Prompts.TutorialInstructionPromptV1.txt";

    private string? _cachedTemplate;

    public int CurrentVersion => LatestVersion;

    public string GetTutorialInstructionPrompt() => _cachedTemplate ??= ReadEmbeddedTemplate();

    private static string ReadEmbeddedTemplate()
    {
        var assembly = typeof(PromptTemplateProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded prompt template resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
