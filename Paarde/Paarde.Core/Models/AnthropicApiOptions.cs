namespace Paarde.Core.Models;

// Configuration for AnthropicApiRunner (T6.3.2) - mirrors ClaudeCliOptions' shape (a record with a
// Default), but there's no CLI executable/arguments here, just the two things a direct Messages API
// call needs that the CLI resolves for itself.
//
// AgentToolsEnabled (Epic 11, F11.1.3) mirrors ClaudeCliOptions.AgentToolsEnabled - same setting,
// read by whichever runner is active, gating this runner's own tool_use loop instead of a CLI flag.
public record AnthropicApiOptions(string ApiKey, string Model, bool AgentToolsEnabled = false)
{
    public static readonly AnthropicApiOptions Default = new(string.Empty, AnthropicModelCatalog.DefaultModelId, false);
}
