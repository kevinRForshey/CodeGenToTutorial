# Paarde
Pronounced like Party, but with a soft t like pardy.
**P**rompt **A**ssist **A**nd **R**eview **D**evelopment **E**nvironment

Paarde is a desktop app that sits between you and the [Claude Code CLI](https://github.com/anthropics/claude-code).
You describe a feature, fix, or new project in plain English; Claude Code does the work; Paarde turns
its response into a readable, step-by-step tutorial and lets you review, edit, and selectively apply
the proposed changes to your project before anything touches disk.

It's built for learning *with* an LLM, not just accepting its output blindly — every change comes
with an explanation, and nothing is written to your files until you choose to apply it.

## What it does

1. **Prompt** — point Paarde at a project folder, describe what you want, optionally attach context
   files, and send it to Claude Code.
2. **Tutorial** — Claude's response comes back as a plain-language explanation of what changed and
   why, broken down per file (and, where possible, per logical step within a file) rather than a
   wall of text.
3. **Diffs** — see exactly what changed, line by line, against what's currently on disk.
4. **Review and edit** — select or deselect individual files or hunks, edit the proposed code directly
   in-app before it's written, and apply only what you want.
5. **Apply, undo, revert** — apply the selected changes to disk, undo the last apply, or revert
   everything the current session has applied — all without leaving the app.
6. **History** — every run is saved. Browse past runs, re-run one as-is, or fork it (reload its
   prompt and working directory for editing) to continue down a different path.

## Why Avalonia (cross-platform by design)

Paarde is built on [Avalonia UI](https://avaloniaui.net/) rather than a Windows-only framework like
WinUI/WPF. Avalonia renders its own UI instead of wrapping each OS's native widget set, so the same
XAML views and C# view models run unmodified on **Windows, Linux, and macOS** — there's one codebase,
not a native app per platform. [FluentAvaloniaUI](https://github.com/amwx/FluentAvalonia) layers a
Fluent/WinUI-style look (navigation shell, icons, dialogs) on top of that, so it looks and feels like
a modern desktop app on any of the three.

## Technology stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| UI framework | [Avalonia](https://avaloniaui.net/) 11.3.x (`Paarde.Avalonia`) |
| UI styling/controls | [FluentAvaloniaUI](https://github.com/amwx/FluentAvalonia) (NavigationView shell, Fluent controls) |
| Code editor | [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) + [AvaloniaEdit.TextMate](https://github.com/AvaloniaUI/AvaloniaEdit) + [TextMateSharp.Grammars](https://github.com/danipen/TextMateSharp) for syntax-highlighted, editable code boxes |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`) |
| DI / hosting | `Microsoft.Extensions.Hosting` generic host, wired up in `Paarde.Avalonia/App.axaml.cs` |
| Domain/shared logic | `Paarde.Core` — parsing, diffing, apply orchestration, session persistence; UI-framework-agnostic so it's unit-testable without Avalonia |
| LLM integration | Shells out to the `claude` CLI (`Paarde.Core/Services/ClaudeCliRunner.cs`), streaming NDJSON (`stream-json`) for live progress |
| Tests | xUnit (`Paarde.Core.Tests`, `Paarde.Avalonia.Tests`) |

### Project layout

```
Paarde.slnx
├─ Paarde.Core/            Domain models, services, view models shared by the UI (no Avalonia dependency)
├─ Paarde.Core.Tests/      Unit tests for Paarde.Core
├─ Paarde.Avalonia/        The desktop app: views, view models, DI setup, Program.cs entry point
└─ Paarde.Avalonia.Tests/  Unit tests for Paarde.Avalonia view models
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- The [Claude Code CLI](https://github.com/anthropics/claude-code) installed and authenticated:
  ```
  npm install -g @anthropic-ai/claude-code
  claude login
  ```
  Paarde shells out to this CLI; it needs to be on your `PATH` (or pointed to explicitly — see
  Settings below) and already logged in.

## Building and running

```bash
# From the repository root
dotnet build Paarde.slnx

# Run the desktop app
dotnet run --project Paarde.Avalonia

# Run the test suites
dotnet test Paarde.Core.Tests
dotnet test Paarde.Avalonia.Tests
```

Paarde runs the same way on Windows, Linux, and macOS — there's no platform-specific build step.

## Usage

### 1. Prompt page

- **Project location** — click **Browse...** to choose the folder Claude Code should treat as its
  working directory. This is the folder it reads from and writes to.
- **Context files** — optionally attach specific files or a whole folder as extra context for the
  model, beyond what's in the working directory.
- **Prompt text** — describe what you want built, fixed, or changed, in plain English. A live
  estimated token count is shown above the box.
- **Preset** — a short instruction nudge appended ahead of your prompt: `General`, `Feature`,
  `Bugfix`, `Refactor`, or `Explain`.
- **Tutorial detail** — controls how deep the generated explanation goes: `Standard`, `Brief`, or
  `Detailed`.
- **Timeout** — how long to wait for the CLI before giving up.
- **Continue previous conversation** — resume the last run's Claude Code session instead of starting
  a new one, so a follow-up prompt has the prior turn's context.
- Click **Send to Claude Code** to run it. A progress ring, elapsed time, and a running character
  count show that a response is streaming in; once it completes you're told how many files are ready
  to review. **Cancel** stops an in-flight run.

### 2. Diffs page

Pick a file from the list on the left to see a color-coded line-by-line diff against what's
currently on disk (or, for a brand-new file, the whole thing shown as additions). **Copy file
contents** copies the proposed content to your clipboard.

### 3. Tutorial page

This is where you actually decide what to keep:

- Pick a file from the list to see its explanation and an **editable** code box — you can tweak
  Claude's proposed content before anything is applied.
- Where the response breaks a file's change into discrete steps, each shows its own title,
  explanation, a checkbox to include/exclude it, and an **Apply this change** / **Explain further**
  button.
- Use **Select all / Select none / Invert selection** per file, or check/uncheck a file in the list
  to include or exclude it entirely.
- **Undo** / **Redo** revert your in-editor edits; **Revert to proposed** discards your edits back to
  Claude's output; **Revert to original** discards both, back to what's on disk right now.
- **Apply Selected** writes every selected change, across every selected file, to disk (with a
  confirmation prompt, and a warning if it would touch files with uncommitted git changes).
- **Undo Last Apply** restores files to their content from right before the most recent apply.
- **Revert All Applied** restores every file this session has touched back to its content from
  before the session started.
- **Refine selected file** sends a scoped follow-up instruction that only asks Claude to change the
  currently selected file, continuing the same conversation.

### 4. History page

Every run is persisted automatically. Select a past session from the list to see its files; use
**Fork** to reload that run's prompt and working directory onto the Prompt page for editing, or
**Re-run** to send it again immediately.

### 5. Settings page

- **Theme** — Light, Dark, or Default (follow the OS).
- **Claude Code CLI** — override the executable path and arguments if `claude` isn't on your `PATH`
  or you need custom flags.
- **Prompt template** — view or override the instruction preamble sent ahead of every prompt;
  **Reset to default** restores the built-in template.

## Project status

Paarde is under active development. See [ROADMAP.md](ROADMAP.md) for a detailed, honest breakdown
of what's implemented, what's partial, and what's planned next.
