# CodeGenToTutorial — State Analysis & Roadmap

**Vision:** An IDE-like desktop app that takes LLM prompts for software development work, turns
the model's output into a readable tutorial, and lets the user pick which code to apply —
editing it first if they want — before the files on disk are updated.

**Analysis date:** 2026-10-01
**Analyzed commit:** `ee2bd23` (+ uncommitted `CodeGenToTutorial.Avalonia/`)

> **2026-10-01 update:** Epic 1 (consolidate onto Avalonia) is done — see the "Progress" note at the
> top of that epic for what changed. The rest of this document still describes the state as of the
> original analysis unless noted.

---

## 1. Where we are today

### 1.1 Solution shape

| Project | TFM | Role | Status |
|---|---|---|---|
| `CodeGenToTutorial.Avalonia` | `net10.0` | Avalonia 11.3.22 + FluentAvaloniaUI desktop app | **Active**; builds clean on Linux |
| `CodeGenToTutorial.Core` | `net10.0` | Shared domain models/services | **Active**; holds the parser, diff builder, CLI runner, apply service |

~~`CodeGenToTutorial` (WinUI 3 / Template Studio app, `net7.0-windows`)~~ — removed 2026-10-01; it was
a Windows-only parallel copy of the Avalonia app that could not build in this environment and forced
every change to be made twice. Its history is still in git (`git log -- CodeGenToTutorial/`) if
anything needs to be recovered.

The previous duplication between `Models/`, `Helpers/`, `Services/`, `ViewModels/` in the two UI
trees has been resolved: the domain layer (`ClaudeCliService`, `ClaudeResponseParser`, `DiffBuilder`,
`FileChangeApplyService`, `PromptResultStore`, and the shared models) now lives once, in
`CodeGenToTutorial.Core`, referenced by the Avalonia app. The Template Studio sample data
(`SampleCompany`, `SampleOrder`, `SampleOrderDetail`, `SampleDataService`) has been deleted from
Core; `IFileService`/`FileService` remain there since `LocalSettingsService` depends on them.

### 1.2 The pipeline that works

The core loop is wired end to end and is genuinely functional:

1. **Prompt page** (`PromptView` / `PromptViewModel`) — folder picker for the project location,
   multi-line prompt box, save-prompt, run button with a progress ring, raw CLI output box. Prompt
   text and working directory persist via `LocalSettingsService`.
2. **CLI invocation** (`ClaudeCliService`) — shells out to `claude -p`, writing the prompt to
   **stdin** (deliberately, to survive embedded newlines), `cmd.exe /c` on Windows to pick up the
   npm shim, direct exec elsewhere. Captures stdout/stderr/exit code as `ClaudeCliResult`.
3. **Protocol** (`PromptViewModel.TutorialInstructionPrompt`) — a hard-coded preamble that asks the
   model to either answer plainly (general question) or emit `## Tutorial` + `## Files`, with
   `### File: <path>` / fenced whole-file content / `#### Tutorial` / `##### Step: <title>` +
   explanation + fenced snippet.
4. **Parsing** (`ClaudeResponseParser`) — two `[GeneratedRegex]` patterns pull out files and per-file
   steps into `ClaudeParsedResponse` → `ProposedFileChange` → `TutorialStep`. Unstructured responses
   fall through as a plain tutorial string with zero files.
5. **Hand-off** (`PromptResultStore`) — singleton in-memory store bridging the transient ViewModels.
6. **Diffs page** — recomputes a line-level diff against the on-disk original using a real LCS
   dynamic-programming table (`DiffBuilder`, with a 4M-cell guard that degrades to whole-file
   replace), master/detail list + color-coded `+`/`-`/` ` lines, copy-file-contents button.
7. **Tutorial page** — master/detail list of files; `TutorialDetailControl` builds a
   `TutorialStepViewModel` per step showing title, explanation, read-only code box, and an
   **"Apply this change"** button that writes the file.

Infrastructure also in place: Generic Host DI, `NavigationView` shell with back-stack
(`NavigationService`/`PageService`/`NavigationViewService`), `INavigationAware` lifecycle, theme
selector + settings page, JSON local settings.

### 1.3 Honest completeness estimate

| Capability from the vision | State |
|---|---|
| Takes LLM prompts for software development | ~70% — works, but single-shot, no sessions/history |
| Turns output into a tutorial | ~55% — per-file steps render; summary is dropped; plain text only |
| Lets the user **select** which code to apply | ~15% — per-*file* only, no selection UI, no hunks |
| Lets the user **make changes** before applying | **0%** — code boxes are `IsReadOnly="True"` |
| Feels like an **IDE** | ~10% — nav shell exists; no file tree, no editor, Main page is empty |
| Safe/reversible file updates | ~10% — blind `File.WriteAllText`, no backup, no undo, no git |

**Overall: a working vertical-slice prototype — roughly 30–35% of the stated product.** The two
headline differentiators (granular selection and editing-before-apply) are the two things that
don't exist yet.

---

## 2. Gap analysis — specific findings

### Blocking the vision

- **No editing of proposed code.** `TutorialDetailControl.axaml` binds the snippet to a
  `TextBox` with `IsReadOnly="True"`, and `ProposedFileChange` is `init`-only immutable. There is no
  path from a user edit back to what gets written.
- **No granular selection.** `FileChangeApplyService.Apply` writes `file.Content` — the entire
  proposed file. `TutorialStepViewModel.Apply` therefore calls `MarkAllStepsApplied()`, which flips
  every step for that file to applied. The per-step "Apply this change" button is, by design today,
  a per-file apply wearing a per-step label (the code comment says so explicitly). No checkboxes,
  no hunk staging, no select-all/none, no "apply all approved".
- **No IDE surface.** `MainView.axaml` is an empty `<Grid>`. There is no project file tree, no
  editor, no tabs, no way to open a file that the LLM didn't mention.

### Correctness / UX bugs

- **The tutorial summary is never shown.** `PromptViewModel` parses `## Tutorial` and stores it in
  `PromptResultStore.Tutorial`, but no view or ViewModel ever reads that property. The overview
  paragraph is silently discarded.
- **Diffs go stale after applying.** `DiffsViewModel.OnNavigatedTo` re-reads the original from disk,
  so once a change is applied the diff collapses to "no changes" with no indication of why, and
  there is no refresh command.
- **Results are lost on restart.** `PromptResultStore` is memory-only.
- **No cancellation.** `IClaudeCliService.RunPromptAsync` accepts a `CancellationToken` but the
  ViewModel never passes one and there is no cancel button or timeout. A hung `claude` hangs the run
  forever with only a spinner.
- **Missing-CLI failure is unhandled.** If `claude` isn't on `PATH`, `Process.Start` throws
  `Win32Exception`, which the generic `catch (Exception)` renders as a raw message. No preflight
  check, no configurable binary path.
- **`ShellView.axaml` emits `AVLN3001`** (no public constructor reachable for the runtime loader).
- **Localization is a stub.** `Strings/Resources.cs` is a hard-coded `Dictionary<string,string>`
  replacing the WinUI `.resw` pipeline; several user-facing strings are inline in `.axaml` anyway.

### Security / safety

- **Path traversal.** `Path.Combine(workingDirectoryPath, file.FilePath)` with an LLM-supplied
  relative path will happily resolve `../../etc/...` or an absolute path and write outside the
  project. There is no canonicalization or containment check.
- **No backup, no undo, no confirmation.** Applying overwrites the working tree destructively with
  no snapshot and no git awareness.

### Protocol fragility

- Whole-file echo costs enormous tokens and breaks down on large files; truncated output silently
  yields a malformed/partial file that would be written verbatim.
- Markdown-regex parsing depends on the model never emitting a language tag after the fence
  (explicitly requested in the prompt) and breaks on nested or triple-backtick content.
- No schema validation, no structured output (JSON / tool calls), no retry on parse failure,
  no streaming — the user stares at a spinner for the whole run.

### Engineering hygiene

- **Zero tests** anywhere in the solution, against a project `ai-governance` skill that mandates a
  90% coverage floor. `DiffBuilder` and `ClaudeResponseParser` are pure, deterministic, and trivially
  testable — this is the cheapest quality win available.
- No CI, no analyzers beyond defaults, no app-level README (both READMEs are Template Studio text).
- `NU1903`: `Tmds.DBus.Protocol` 0.20.0 (transitive via Avalonia 11.2.1) has a known high-severity
  advisory. Mixed TFMs (`net7.0` Core ← `net10.0` app) and stale packages
  (`Microsoft.Extensions.Hosting` 8.0.1 on a net10 target).

---

## 3. Epics, Features & Tasks

Priority: **P0** = required for the vision to exist · **P1** = required to be usable/trustworthy ·
**P2** = polish & scale.

---

### EPIC 1 — Consolidate the codebase onto one UI stack *(P0)* — ✅ Done 2026-10-01

*Why first: every item below would otherwise have to be built twice.*

**F1.1 — Pick and commit to a single UI target** ✅
- [x] T1.1.1 Decide: Avalonia-only (cross-platform, builds today) vs WinUI. **Decision: Avalonia.**
- [x] T1.1.2 Remove the WinUI project; dropped from `CodeGenToTutorial.slnx` (history preserved in git).
- [x] T1.1.3 Delete the duplicated `Models`/`Helpers`/`Services`/`ViewModels` — moot, whole tree removed.

**F1.2 — Make `.Core` the real shared domain library** ✅
- [x] T1.2.1 Retargeted `.Core` to `net10.0`; package versions aligned via `Directory.Build.props`.
- [x] T1.2.2 Moved `ProposedFileChange`, `TutorialStep`, `DiffLine`, `ClaudeCliResult`,
  `ClaudeParsedResponse` into `.Core/Models`.
- [x] T1.2.3 Moved `DiffBuilder`, `ClaudeResponseParser`, `ClaudeCliService`, `FileChangeApplyService`,
  `PromptResultStore` into `.Core` behind their interfaces; the Avalonia app now only holds
  Avalonia-specific concerns (nav, theming, local settings, folder picker, converters).
- [x] T1.2.4 Deleted the Template Studio sample-data types (`SampleCompany`, `SampleOrder`,
  `SampleOrderDetail`, `SampleDataService`) and `ISampleDataService`. `IFileService`/`FileService`
  were kept — `LocalSettingsService` genuinely depends on them.

**F1.3 — Clean the build** ✅
- [x] T1.3.1 Fixed `AVLN3001` on `ShellView` — it took a DI constructor parameter unlike every other
  view; changed to the same parameterless `App.GetService<T>()` pattern the rest of the views use.
- [x] T1.3.2 Bumped Avalonia 11.2.1 → **11.3.22** and FluentAvaloniaUI 2.2.0 → **2.5.1** (same major,
  avoiding the untested Avalonia 12 / FluentAvaloniaUI 3 breaking changes) — resolves the
  `Tmds.DBus.Protocol` `NU1903` advisory. Also bumped `CommunityToolkit.Mvvm` → 8.4.2 and
  `Microsoft.Extensions.Hosting` → 10.0.12 to match the `net10.0` target.
- [x] T1.3.3 Added `Directory.Build.props` at the solution root (`Nullable`, `ImplicitUsings`,
  `LangVersion`, analyzers) shared by both remaining projects. Enabling `Nullable` on `.Core`
  surfaced two latent warnings in the old Template Studio `Json`/`FileService` helpers; fixed them
  (nullable return/parameter annotations) rather than suppress.

Verified: `dotnet build CodeGenToTutorial.slnx` from a clean `bin`/`obj` → **0 warnings, 0 errors**;
app launches (`dotnet run --project CodeGenToTutorial.Avalonia`) without throwing. Nothing is
committed yet — review `git status`/`git diff` before committing.

---

### EPIC 2 — Editable proposed changes *(P0 — the biggest missing piece)*

**F2.1 — Make the change model mutable and observable**
- T2.1.1 Replace `ProposedFileChange` (init-only) with an observable
  `ProposedFileChangeViewModel` holding `OriginalContent`, `ProposedContent`, `EditedContent`,
  and `IsDirty`.
- T2.1.2 Introduce a per-step `ProposedHunk` model (step title, explanation, snippet, resolved
  location in the file, selection state) so steps stop being presentational-only.
- T2.1.3 Make `PromptResultStore` hold the mutable graph and raise change notifications.

**F2.2 — In-app code editor**
- T2.2.1 Evaluate and add an editor control (AvaloniaEdit is the obvious candidate) — syntax
  highlighting, line numbers, bracket matching.
- T2.2.2 Replace the read-only `TextBox` in `TutorialDetailControl` with an editable editor bound
  to `EditedContent`.
- T2.2.3 Dirty tracking, revert-to-proposed, and revert-to-original per file.
- T2.2.4 Undo/redo within the editor; warn on navigating away with unsaved edits.

**F2.3 — Write the user's edits, not the model's output**
- T2.3.1 Change `IFileChangeApplyService.Apply` to take the effective (possibly edited) content.
- T2.3.2 Re-diff live as the user edits so the Diffs page reflects edits, not just the raw proposal.

---

### EPIC 3 — Granular selection & apply *(P0)*

**F3.1 — Hunk-level diff model**
- T3.1.1 Extend `DiffBuilder` to group `DiffLine`s into hunks with context (unified-diff style).
- T3.1.2 Map each `TutorialStep` snippet onto the hunk(s) it produces, so "apply this step" means
  something precise.
- T3.1.3 Implement patch application: compose the target file content from the selected hunks plus
  the untouched original.

**F3.2 — Selection UI**
- T3.2.1 Checkbox per file in the Diffs and Tutorial master lists.
- T3.2.2 Checkbox per hunk/step in the detail pane, with per-file tri-state roll-up.
- T3.2.3 Select-all / select-none / invert; show a count of selected changes.
- T3.2.4 Persist selection state across navigation between the Diffs and Tutorial pages.

**F3.3 — Apply orchestration**
- T3.3.1 "Apply selected" command with a pre-apply summary (N files, M hunks).
- T3.3.2 Per-file and per-hunk applied state that is *accurate* — remove the
  `MarkAllStepsApplied()` workaround.
- T3.3.3 Conflict detection: if a file changed on disk since the proposal, warn and offer re-diff.
- T3.3.4 Partial-failure reporting — don't abandon the batch on one failed write.

---

### EPIC 4 — Safe, reversible file updates *(P1)*

**F4.1 — Containment & validation**
- T4.1.1 Canonicalize with `Path.GetFullPath` and reject any target outside the working directory;
  reject absolute and rooted LLM paths. **(Security — do this early.)**
- T4.1.2 Reject writes to `.git/`, `bin/`, `obj/`, and anything matched by `.gitignore` unless
  explicitly confirmed.
- T4.1.3 Preserve the file's existing encoding and line endings instead of defaulting.

**F4.2 — Backup & undo**
- T4.2.1 Snapshot each file before first write in a session-scoped backup store.
- T4.2.2 "Undo last apply" and "Revert all applied changes in this session".
- T4.2.3 Apply-history log surfaced in the UI.

**F4.3 — Git integration**
- T4.3.1 Detect a git repo at the working directory; show dirty state before applying.
- T4.3.2 Warn when applying over uncommitted work.
- T4.3.3 Optional: stage applied files, or create a branch/commit per apply batch.

---

### EPIC 5 — Harden the LLM protocol *(P1)*

**F5.1 — Structured output instead of markdown scraping**
- T5.1.1 Move the response contract to JSON (or CLI structured output) with an explicit schema.
- T5.1.2 Validate against the schema; on failure, retry once with the error fed back.
- T5.1.3 Keep the markdown parser as a fallback and cover it with tests.

**F5.2 — Diffs over the wire, not whole files**
- T5.2.1 Ask for unified diffs / search-replace edit blocks instead of full file contents to cut
  tokens and avoid truncation.
- T5.2.2 Apply those patches locally with fuzz/context matching.
- T5.2.3 Detect truncated or incomplete responses and refuse to apply them.

**F5.3 — Prompt template management**
- T5.3.1 Move `TutorialInstructionPrompt` out of `PromptViewModel` into a versioned template file.
- T5.3.2 Let the user view and override the system preamble in Settings.
- T5.3.3 Named prompt presets (feature / bugfix / refactor / explain) with a tutorial-depth control.

**F5.4 — Context selection**
- T5.4.1 Let the user attach specific files/folders as context for the prompt.
- T5.4.2 Show an estimated token count before sending.

---

### EPIC 6 — LLM runner robustness *(P1)*

**F6.1 — Lifecycle control**
- T6.1.1 Cancel button wired to a real `CancellationTokenSource`; kill the child process on cancel.
- T6.1.2 Configurable timeout with a clear timeout message.
- T6.1.3 Preflight check that the CLI exists; actionable error with install guidance if not.
- T6.1.4 Configurable CLI path/arguments in Settings.

**F6.2 — Streaming & progress**
- T6.2.1 Stream stdout into the output pane as it arrives rather than buffering to completion.
- T6.2.2 Parse incrementally so files appear as they are emitted.
- T6.2.3 Elapsed-time and token/cost readout.

**F6.3 — Provider abstraction**
- T6.3.1 Abstract behind an `ILlmRunner` so the CLI is one implementation.
- T6.3.2 Add a direct Anthropic API runner (API key in secure storage, model selection).
- T6.3.3 Model picker in Settings.

---

### EPIC 7 — Sessions, history & persistence *(P1)*

**F7.1 — Persist runs**
- T7.1.1 Persist each run (prompt, raw output, parsed result, selections, apply log) to disk.
- T7.1.2 Restore the last session on launch.

**F7.2 — History UI**
- T7.2.1 History page listing past runs with timestamp, prompt excerpt, and file count.
- T7.2.2 Reopen a past run's tutorial and diffs read-only.
- T7.2.3 Re-run or fork a past prompt.

**F7.3 — Multi-turn conversation**
- T7.3.1 Follow-up prompts that carry the prior turn's context (CLI session resume or API history).
- T7.3.2 "Refine this file" / "explain this step further" inline actions that issue a scoped
  follow-up.
- T7.3.3 Ask-a-question-about-this-change affordance inside the tutorial.

---

### EPIC 8 — Make it feel like an IDE *(P1/P2)*

**F8.1 — Project explorer**
- T8.1.1 File-tree view of the working directory with gitignore-aware filtering.
- T8.1.2 Badges marking files touched by the current proposal.
- T8.1.3 Open any file in the editor, proposed or not.

**F8.2 — Shell & workspace**
- T8.2.1 Replace the empty `MainView` with a real dashboard/landing page (recent projects, recent
  runs, quick prompt).
- T8.2.2 Tabbed editing of multiple files.
- T8.2.3 Resizable/collapsible panes; persist layout.
- T8.2.4 Recent-projects list and fast project switching.

**F8.3 — Better diff viewer**
- T8.3.1 Side-by-side mode in addition to inline.
- T8.3.2 Collapse unchanged regions with expand-context controls.
- T8.3.3 Intra-line (word-level) highlighting.
- T8.3.4 Syntax highlighting inside the diff; next/previous-change navigation.

**F8.4 — Tutorial presentation**
- T8.4.1 **Display the `## Tutorial` overview that is currently parsed and thrown away.**
- T8.4.2 Render markdown in explanations (code spans, lists, emphasis) instead of flat text.
- T8.4.3 Step numbering, progress indicator, and prev/next step navigation.
- T8.4.4 Export the tutorial to markdown/HTML.
- T8.4.5 Jump from a tutorial step to the corresponding hunk in the diff viewer, and back.

---

### EPIC 9 — Quality, testing & delivery *(P1 — required by project governance)*

**F9.1 — Test foundation**
- T9.1.1 Add an xUnit test project; wire coverage reporting toward the 90% floor.
- T9.1.2 `DiffBuilder` tests: empty/identical/insert/delete/move, CRLF vs LF, the `MaxLcsCells`
  fallback.
- T9.1.3 `ClaudeResponseParser` tests: happy path, no-files response, unstructured response,
  language tags on fences, nested fences, multiple files, multiple steps, truncated input.
- T9.1.4 `FileChangeApplyService` tests: new file, nested directory creation, **path-traversal
  rejection**, permission failure.
- T9.1.5 ViewModel tests with a faked `ILlmRunner`.

**F9.2 — Reliability**
- T9.2.1 Structured logging (`ILogger`) across services; user-accessible log file.
- T9.2.2 Replace broad `catch (Exception)` with specific handling and a global crash handler.
- T9.2.3 Settle `async void` navigation handlers (`OnNavigatedTo`, `InitializeThemeAndNavigate`).

**F9.3 — Delivery**
- T9.3.1 CI: build + test + coverage gate on push.
- T9.3.2 Replace the Template Studio READMEs with real docs (what it is, prerequisites, how to run).
- T9.3.3 Packaging/publish per platform.
- T9.3.4 Finish or remove the half-stubbed localization layer.

---

## 4. Suggested execution order

1. **Epic 1** — one codebase, or everything after this costs double.
2. **T4.1.1** — the path-traversal fix. Small, and it is currently a real write-anywhere hole.
3. **F9.1** — tests on `DiffBuilder` + `ClaudeResponseParser` before refactoring them under Epic 2/3.
4. **Epic 2 + Epic 3 together** — editable content and hunk selection share the same model rework;
   splitting them means rewriting `ProposedFileChange` twice. **This is where the product actually
   becomes the thing described in the vision.**
5. **Epic 4** (backup/undo) and **Epic 6** (cancel, timeout, preflight) — make it trustworthy.
6. **Epic 5** — structured output and diff-based edits; unlocks real-world file sizes.
7. **Epic 7** — sessions and multi-turn.
8. **Epic 8** — the IDE surface and presentation polish.
9. **Epic 9** remaining — CI, docs, packaging.
