namespace Paarde.Core.Models;

// Configuration for AnthropicApiRunner (T6.3.2) - mirrors ClaudeCliOptions' shape (a record with a
// Default), but there's no CLI executable/arguments here, just the two things a direct Messages API
// call needs that the CLI resolves for itself.
public record AnthropicApiOptions(string ApiKey, string Model)
{
    public static readonly AnthropicApiOptions Default = new(string.Empty, AnthropicModelCatalog.DefaultModelId);
}
