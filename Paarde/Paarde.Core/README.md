# Paarde.Core

The domain/shared-logic library behind [Paarde](../README.md) — parsing Claude's structured JSON
response, building hunk-level diffs, orchestrating applies (containment checks, backups, undo/revert,
git status), and persisting run history. It has no dependency on Avalonia or any other UI framework
(only `Microsoft.Extensions.Logging.Abstractions`, for the optional `ILogger<T>` services here accept),
so it's fully unit-testable without a UI and could back a different front end without changes.

`Paarde.Avalonia` is the only consumer today; its view models call into this project's services rather
than duplicating any of this logic.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Building and testing

```bash
# From the repository root
dotnet build Paarde.Core

# Run this project's unit tests (Paarde.Core.Tests)
dotnet test Paarde.Core.Tests
```

See the [root README](../README.md) for how to build and run the full Paarde desktop app, and
[ROADMAP.md](../ROADMAP.md) for what's implemented here versus still planned.
