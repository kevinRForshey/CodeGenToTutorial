namespace CodeGenToTutorial.Core.Contracts.Services;

// Separates "what instructions get sent to the LLM" from PromptViewModel so the template can be
// edited (and, eventually, overridden per T5.3.2) without touching ViewModel code, and so a new
// contract revision can ship as a new versioned resource rather than mutating the one every past
// response was parsed against.
public interface IPromptTemplateProvider
{
    // The template version that matches the JSON schema ClaudeJsonResponseParser expects today;
    // bump this (and add a new TutorialInstructionPromptV{N}.txt) whenever that schema changes.
    int CurrentVersion { get; }

    string GetTutorialInstructionPrompt();
}
