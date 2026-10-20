# Paarde — State Analysis & Roadmap

> **2026-10-18 update:** Renamed from `CodeGenToTutorial` to **Paarde** (Prompt Assist And Review
> Development Environment) across every project, namespace, and the solution file. Done-notes below
> that predate the rename still reference the old `CodeGenToTutorial.*` paths/namespaces, since those
> are an accurate record of what existed at the time each task was completed — not retroactively
> rewritten, same reasoning as not rewriting git history.

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
- [x] T2.1.1 Replace `ProposedFileChange` (init-only) with an observable
  `ProposedFileChangeViewModel` holding `OriginalContent`, `ProposedContent`, `EditedContent`,
  and `IsDirty`. — done 2026-10-06: new class lives in `CodeGenToTutorial.Core/ViewModels`
  (Core now takes a `CommunityToolkit.Mvvm` dependency so `PromptResultStore` — the shared graph
  both the Tutorial and Diffs pages read from — can hold observable items directly).
  `FilePath`/`OriginalContent`/`ProposedContent`/`TutorialSteps`/`DiffLines` are set at construction;
  `EditedContent` defaults to `ProposedContent` and `IsDirty` flips automatically when it diverges.
  Nothing mutates `EditedContent` yet (no editor exists — that's F2.2), and
  `FileChangeApplyService.Apply` still writes `ProposedContent`, so behavior is unchanged; this is
  scaffolding for T2.2.2/T2.3.1. Updated every consumer (`ClaudeResponseParser`, `PromptResultStore`,
  `FileChangeApplyService`, `DiffsViewModel`, `TutorialViewModel`, `TutorialStepViewModel`, the
  Diffs/Tutorial `.axaml` views and code-behind) and all existing tests; added
  `ProposedFileChangeViewModelTests` covering the dirty-tracking behavior. Full solution builds
  with 0 warnings/errors, all 46 tests pass, app launches without throwing.
- [x] T2.1.2 Introduce a per-step `ProposedHunk` model (step title, explanation, snippet, resolved
  location in the file, selection state) so steps stop being presentational-only. — done
  2026-10-06: `ProposedHunk` (`CodeGenToTutorial.Core/ViewModels`) carries `Title`/`Explanation`/
  `Snippet` plus an observable `IsSelected` (defaults `true`, nothing reads it yet — scaffolding
  for F3.2) and a `Location` (`HunkLocation`, start/end line numbers within `ProposedContent`)
  resolved by a new `HunkLocationResolver` helper that does an exact line-sequence search; returns
  `null` if the snippet isn't found verbatim (known fragility, same class as the nested-fence
  parsing issue — a baseline for Epic 5, not a bug to fix here). `ProposedFileChangeViewModel`'s
  `TutorialSteps` property is renamed to `Hunks` and now holds `ProposedHunk` instead of the raw
  `TutorialStep`; `ClaudeResponseParser` builds hunks (and resolves their location) right after
  parsing each file's content. `TutorialStepViewModel` now takes a `ProposedHunk` instead of a
  `TutorialStep` — display behavior (`Title`/`Explanation`/`Code`) is unchanged. Added
  `HunkLocationResolverTests`. Full solution builds clean, 52/52 tests pass, app launches without
  throwing.
- [x] T2.1.3 Make `PromptResultStore` hold the mutable graph and raise change notifications. — done
  2026-10-08: the store already held the shared mutable `ProposedFileChangeViewModel` graph (since
  T2.1.1/T2.3.2), but it was a plain POCO — no way for anything to react to a new prompt result
  except a page's `OnNavigatedTo` happening to re-run. `PromptResultStore` is now `partial class
  PromptResultStore : ObservableObject, IPromptResultStore`: `Tutorial`/`WorkingDirectoryPath` are
  `[ObservableProperty]` fields (raise `PropertyChanged`), and `Files` is a fixed
  `ObservableCollection<ProposedFileChangeViewModel>` instance — `SetResult` now `Clear()`s and
  re-`Add()`s into it rather than reassigning the reference, so a subscriber holding the original
  `Files` collection keeps seeing `CollectionChanged` across repeated prompt runs instead of
  watching a now-stale snapshot. `IPromptResultStore` extends `INotifyPropertyChanged` and exposes
  `Files` as the concrete `ObservableCollection` (was `IReadOnlyList`) so consumers can subscribe to
  either. `TutorialViewModel`/`DiffsViewModel` need no changes — their `OnNavigatedTo` already just
  enumerates `_promptResultStore.Files`, which works unchanged against the new collection type.
  Updated `FakePromptResultStore` to match the interface and added
  `CodeGenToTutorial.Core.Tests/Services/PromptResultStoreTests.cs` (stores values, raises
  `PropertyChanged` for `Tutorial`/`WorkingDirectoryPath`, raises `CollectionChanged` on `Files`,
  and the same `Files` instance survives a second `SetResult`). Full solution builds clean,
  65/65 tests pass (61 existing + 4 new). Not wired to a new UI consumer yet — nothing currently
  binds directly to the store outside the existing `OnNavigatedTo` pull, so this is the
  notification plumbing the rest of F2.1/Epic 3 (and the still-open "tutorial summary is never
  shown" bug from the gap analysis) can build on, not a new visible behavior by itself.

**F2.2 — In-app code editor**
- [x] T2.2.1 Evaluate and add an editor control (AvaloniaEdit is the obvious candidate) — syntax
  highlighting, line numbers, bracket matching. — done 2026-10-06: added `Avalonia.AvaloniaEdit`
  11.4.0 (+ `AvaloniaEdit.TextMate`/`TextMateSharp.Grammars` for TextMate-based highlighting); its
  `Avalonia` dependency floor is 11.0.0 so it stays on the pinned 11.3.22 line. New `CodeEditor`
  control (`CodeGenToTutorial.Avalonia/Controls`) wraps `AvaloniaEdit.TextEditor`: turns on
  `ShowLineNumbers`, installs TextMate with a grammar picked from a bound `FilePath`'s extension,
  and adds bracket matching via a new `BracketHighlightRenderer` (`IBackgroundRenderer`) since
  AvaloniaEdit has none built in — it's a language-agnostic depth-counting scan of the raw text
  for a `()`/`[]`/`{}` pair touching the caret, not grammar-aware, so it doesn't skip brackets
  inside strings/comments (same class of known fragility as the response-parser issues, acceptable
  for a code viewer). Because `TextEditor.Text` is a plain CLR property rather than an
  `AvaloniaProperty`, added a `TextEditorExtensions.BindableText` attached property so it can still
  be the target of an XAML `Binding`. `TutorialDetailControl`'s per-step `TextBox` is replaced with
  `CodeEditor` (still `IsReadOnly="True"` here; wiring it to `EditedContent` for in-place editing is
  T2.2.2). `TutorialStepViewModel` gained a `FilePath` property (from its `ProposedFileChangeViewModel`)
  so the control has an extension to pick a grammar from. Added `BracketHighlightRendererTests`
  against the extracted matching algorithm (internal, `InternalsVisibleTo` the Avalonia test
  project). Full solution builds clean, 57/57 tests pass. Not visually verified in a browser/running
  app — this sandbox has no X server (`XOpenDisplay failed`, no `Xvfb`), so the editor's rendering
  (line numbers, highlighting colors, bracket highlight) is unverified beyond compiling and the
  unit-tested matching logic.
- [x] T2.2.2 Replace the read-only `TextBox` in `TutorialDetailControl` with an editable editor bound
  to `EditedContent`. — done 2026-10-07: `FileChangeApplyService.Apply` writes a file's whole
  `ProposedContent`, not any one step's snippet, so there's exactly one editable buffer per file,
  not per step. Moved the `CodeEditor` out of the per-step `DataTemplate` and hoisted it to file
  scope in `TutorialDetailControl` (one instance, `IsReadOnly="False"`, bound
  `EditedContent, Mode=TwoWay`); each step card below it now only shows `Title`/`Explanation`/
  `Apply`. Made `TextEditorExtensions.BindableText` round-trip: it now hooks the editor's
  `TextChanged` once per instance and pushes `editor.Text` back onto the attached property (the
  attached property's default binding mode is `TwoWay`), and only reassigns `editor.Text` when the
  incoming value actually differs — needed because `TextEditor.Text`'s setter unconditionally
  resets `CaretOffset` to 0 and clears the undo stack, which would fight the user's typing on every
  keystroke otherwise. Removed `TutorialStepViewModel.Code`/`FilePath` (dead now that the per-step
  snippet view is gone). Tried adding a test that constructs a real `AvaloniaEdit.TextEditor` to
  exercise the two-way bridge directly; it throws (`CaretNavigationCommandHandler`'s static
  keymap init needs a running Avalonia `Application`), and this project has no
  `Avalonia.Headless` test host, so that's unverified by a test — reviewed by hand instead. Full
  solution builds clean, 57/57 tests pass. Not visually verified (same no-X-server sandbox
  limitation noted under T2.2.1); applying edited-but-unsaved content is still a no-op until
  T2.3.1 rewires `Apply` to use `EditedContent` instead of `ProposedContent`.
- [x] T2.2.3 Dirty tracking, revert-to-proposed, and revert-to-original per file. — done 2026-10-08:
  dirty tracking already existed (`IsDirty`, from T2.1.1). Added the two revert actions as
  `[RelayCommand]`s directly on `ProposedFileChangeViewModel` (`CommunityToolkit.Mvvm.Input`,
  already available via the `CommunityToolkit.Mvvm` package `.Core` already depends on) rather than
  on a page ViewModel, since the state being reverted (`EditedContent`) and the state being
  reverted *to* (`ProposedContent`/`OriginalContent`) all live on that one object already:
  `RevertToProposedCommand` sets `EditedContent = ProposedContent` and is disabled once
  `!IsDirty` (nothing to discard); `RevertToOriginalCommand` sets `EditedContent =
  OriginalContent` and is disabled once `EditedContent == OriginalContent` - deliberately *not*
  gated on `IsDirty`, so a not-dirty file whose `ProposedContent` still differs from
  `OriginalContent` can still be rejected outright. Both commands' `CanExecute` are kept correct
  via existing `On*Changed` partial hooks (`OnIsDirtyChanged` →
  `RevertToProposedCommand.NotifyCanExecuteChanged()`; `OnEditedContentChanged`/
  `OnOriginalContentChanged` → `RevertToOriginalCommand.NotifyCanExecuteChanged()`), same pattern
  `PromptViewModel` already uses for `RunPromptCommand`. Wired to two new buttons in
  `TutorialDetailControl.axaml`, above the per-file `CodeEditor` (the only place editing happens,
  per T2.2.2) alongside an "Edited" label bound to `IsDirty`; the Diffs page is unchanged since it
  has no editor to revert. Added 8 new tests to `ProposedFileChangeViewModelTests` covering both
  commands' `CanExecute` gating and effect, including the not-dirty-but-still-revertible-to-original
  case. Full solution builds clean, 73/73 tests pass (65 existing + 8 new). Not visually verified
  (no X server in this sandbox, same limitation noted on prior Epic 2 tasks).
- [x] T2.2.4 Undo/redo within the editor; warn on navigating away with unsaved edits. — done 2026-10-08:
  investigated AvaloniaEdit via reflection on the compiled `Avalonia.AvaloniaEdit` assembly (doc
  searches kept 404ing on exact source lines) and found `TextEditor` already has public
  `Undo()`/`Redo()`/`CanUndo`/`CanRedo`, and that `TextAreaDefaultInputHandler` (installed on every
  `TextArea` by default) already wires Ctrl+Z/Ctrl+Y to them — so keyboard undo/redo needed zero
  code. The only real gap was discoverability: added named `UndoButton`/`RedoButton` buttons to
  `TutorialDetailControl.axaml` next to the revert buttons, wired to `Editor.Undo()`/`Editor.Redo()`
  in the code-behind. `CanUndo`/`CanRedo` are plain CLR properties (not `AvaloniaProperty`s), so
  they can't be bound from XAML directly; kept them in sync via the editor's public `TextChanged`
  event (fires on typing *and* on Undo/Redo itself), re-checking both buttons' `IsEnabled` each
  time. `TutorialDetailControl` is a single long-lived instance whose `DataContext` rebinds per
  file (per T2.2.1/T2.2.2), so there's one `Document`/`UndoStack` for the page's lifetime; switching
  files resets undo history for the new file, which is expected.
  For the navigate-away warning: found via reflection that FluentAvalonia's `Frame` has a
  cancelable `Navigating` event (`NavigatingCancelEventArgs.Cancel` is settable) that fires *before*
  content changes - unlike `Navigated`/`INavigationAware.OnNavigatedFrom`, which both only run
  *after* navigation already happened, so neither could actually block anything. Added
  `IConfirmNavigationAway` (opt-in interface, `bool HasUnsavedChanges`) implemented by
  `TutorialViewModel` as `Files.Any(f => f.IsDirty)`, and a new `IDialogService`/`DialogService`
  wrapping FluentAvalonia's `ContentDialog` for a generic yes/no confirm (`DefaultButton =
  ContentDialogButton.Close` so Enter/default focus can't accidentally pick the destructive
  choice). `NavigationService` now subscribes to `Frame.Navigating`: if the leaving page reports
  unsaved changes, cancels synchronously and shows the confirm dialog; on "leave without saving" it
  re-issues the exact same navigation (forward or `GoBack`) with a `_suppressUnsavedChangesCheck`
  flag set so the retry doesn't re-trigger the same prompt. Refactored `NavigateTo` to share its
  core logic with the retry path via a private `NavigateToPageType` helper. Also fixed a
  pre-existing latent bug found while doing this: `GoBack()` previously called
  `OnNavigatedFrom()`/returned `true` unconditionally, even though `Frame.GoBack()` returns `void`
  and could be silently cancelled by the new `Navigating` handler - it now compares
  `GetPageViewModel()` before/after the call to detect whether navigation actually happened.
  Registered `IDialogService` in `App.axaml.cs`'s DI container. Added `TutorialViewModelTests`
  (new file) covering `HasUnsavedChanges` across no files / clean files / a dirtied file / a
  dirtied-then-reverted file. Did not add tests for `NavigationService`'s new `Navigating` handling
  or `DialogService`'s `ContentDialog` usage - both need a real FluentAvalonia `Frame`/`ContentDialog`
  instance, which (consistent with the AvaloniaEdit `TextEditor` limitation noted under T2.2.2)
  can't be constructed without a running Avalonia `Application` and headless test host this project
  doesn't have; reviewed by hand instead. Full solution builds clean, 77/77 tests pass (73 existing
  + 4 new). Not visually verified (no X server in this sandbox, same limitation noted on prior Epic
  2 tasks) - in particular, the undo/redo buttons' enabled state and the confirm dialog's actual
  on-screen appearance are unverified beyond code review.

**F2.3 — Write the user's edits, not the model's output**
- [x] T2.3.1 Change `IFileChangeApplyService.Apply` to take the effective (possibly edited) content. —
  done 2026-10-07: `ProposedFileChangeViewModel` already modeled the effective content as
  `EditedContent` (defaults to `ProposedContent`, diverges once the user types in the `CodeEditor`
  from T2.2.2), so no interface/signature change was needed — `FileChangeApplyService.Apply` now
  writes `file.EditedContent` instead of `file.ProposedContent`. Added
  `Apply_UserEditedContent_WritesEditedContentNotProposedContent` to pin the behavior; all existing
  tests still pass unchanged since they never diverge `EditedContent` from `ProposedContent`. Full
  solution builds clean, 58/58 tests pass. Known follow-on gap, not fixed here: the Diffs page
  (`DiffsViewModel.OnNavigatedTo`) builds its *own* `ProposedFileChangeViewModel` instances from
  `file.ProposedContent`, separate from the ones the Tutorial page edits, so Diffs still shows the
  raw proposal rather than live edits — that's T2.3.2.
- [x] T2.3.2 Re-diff live as the user edits so the Diffs page reflects edits, not just the raw
  proposal. — done 2026-10-07: `DiffsViewModel.OnNavigatedTo` used to build its own throwaway
  `ProposedFileChangeViewModel` copies from `file.ProposedContent`, separate from the instances the
  Tutorial page edits, so Diffs could never see live edits regardless of how the diff was computed.
  Fixed by having Diffs reuse the store's own instances (same objects as Tutorial, matching the
  pattern `TutorialViewModel` already used) and moving diff computation *into*
  `ProposedFileChangeViewModel` itself: `DiffLines` is now a computed `[ObservableProperty]`
  recomputed via `DiffBuilder` whenever `OriginalContent` or `EditedContent` change (constructor
  computes the initial value; `OnEditedContentChanged`/`OnOriginalContentChanged` recompute it).
  `OriginalContent` changed from a get-only constructor param to a settable `[ObservableProperty]`
  so Diffs can back-fill it from disk on navigation without re-constructing the object. Net effect:
  typing in the Tutorial page's `CodeEditor` now updates the Diffs page's diff immediately, since
  both pages are watching the same `EditedContent`-driven `DiffLines` on the same instance. Added
  `ProposedFileChangeViewModelTests` cases for initial/edited/original-driven recompute. Full
  solution builds clean, 61/61 tests pass. Not visually verified (no X server in this sandbox, same
  limitation as T2.2.1/T2.2.2) — confirmed by test and by reading the binding (`DiffsDetailControl`
  binds `DiffLines` via a compiled binding, which already refreshes on `PropertyChanged`).

---

### EPIC 3 — Granular selection & apply *(P0)*

**F3.1 — Hunk-level diff model**
- [x] T3.1.1 Extend `DiffBuilder` to group `DiffLine`s into hunks with context (unified-diff style). —
  done 2026-10-10: `DiffBuilder.GroupIntoHunks` (+ new `DiffHunk` model) groups a flat line diff into
  unified-diff-style hunks with configurable context, merging change regions whose gap is within
  `2 * contextLines`. Not consumed by anything yet - scaffolding for T3.1.2/T3.1.3.
- [x] T3.1.2 Map each `TutorialStep` snippet onto the hunk(s) it produces, so "apply this step" means
  something precise. — done 2026-10-10: new `ProposedHunkMatcher.FindMatches` maps a `ProposedHunk`'s
  resolved `HunkLocation` onto the `DiffHunk`(s) whose proposed-side range overlaps it (pure-deletion
  hunks, which contribute no proposed lines, never match). `ProposedFileChangeViewModel` now computes
  `DiffHunks` reactively alongside `DiffLines`, and keeps every `Hunk`'s new `MatchedDiffHunks`
  property in sync whenever `DiffHunks` changes (edits, or the `Hunks` list itself being assigned).
  Still display-only - nothing reads `MatchedDiffHunks` yet; that's T3.1.3/F3.2.
- [x] T3.1.3 Implement patch application: compose the target file content from the selected hunks plus
  the untouched original. — done 2026-10-11: `DiffHunk` gained `SourceRangeStart`/`SourceRangeEnd`
  (its `[start, end)` index range within the `DiffLines` it was grouped from) so a hunk's lines can be
  distinguished from its neighbors' when recomposing. New `DiffBuilder.ComposeContent(diffLines,
  hunks, isSelected)` walks the full line list once: inside a selected hunk's range it keeps
  `Unchanged`/`Added` lines (the proposed outcome); inside an unselected hunk's range it keeps
  `Unchanged`/`Removed` lines (reverts that region to the original); everything outside any hunk's
  range is guaranteed `Unchanged` (hunks are built to cover every changed region) and passes through
  either way. Pure algorithm, not wired to `ProposedFileChangeViewModel`/UI - there's no selection
  state on `DiffHunk` itself yet (only per-step `ProposedHunk.IsSelected`), and deciding how step
  selection aggregates onto a shared `DiffHunk` (e.g. two steps mapping to the same hunk with
  different selection) is F3.2/F3.3's job, not this one's.

**F3.2 — Selection UI**
- [x] T3.2.1 Checkbox per file in the Diffs and Tutorial master lists. — done 2026-10-12: tri-state
  `CheckBox` bound to `ProposedFileChangeViewModel.IsSelected` in both `DiffsView` and `TutorialView`'s
  master `ListBox` item templates.
- [x] T3.2.2 Checkbox per hunk/step in the detail pane, with per-file tri-state roll-up. — done
  2026-10-12: per-step checkbox in `TutorialDetailControl` bound to `ProposedHunk.IsSelected`;
  `ProposedFileChangeViewModel.IsSelected` rolls up to `true`/`false`/`null` depending on whether
  all, none, or some of a file's hunks are selected.
- [x] T3.2.3 Select-all / select-none / invert; show a count of selected changes. — done 2026-10-12:
  `SelectAllHunksCommand`/`SelectNoneHunksCommand`/`InvertHunkSelectionCommand` on
  `ProposedFileChangeViewModel`, surfaced as buttons in `TutorialDetailControl`; `SelectionSummary`
  shows the selected/total hunk count in the master list.
- [x] T3.2.4 Persist selection state across navigation between the Diffs and Tutorial pages. — done
  2026-10-12: both pages read the same `ProposedFileChangeViewModel` instances straight out of
  `IPromptResultStore.Files` rather than copying them, so selection is shared by construction; pinned
  by `SelectionPersistenceTests`.

**F3.3 — Apply orchestration**
- [x] T3.3.1 "Apply selected" command with a pre-apply summary (N files, M hunks). — done 2026-10-12:
  new `IApplyOrchestrationService.Preview` computes a file/change count across every file in the
  tutorial (not just the one shown in the detail pane); `TutorialViewModel.ApplySelectedCommand`
  shows it in a confirm dialog before calling `ApplySelected`, and a "nothing selected" message if
  there's nothing to apply.
- [x] T3.3.2 Per-file and per-hunk applied state that is *accurate* — remove the
  `MarkAllStepsApplied()` workaround. — done 2026-10-12: `TutorialDetailControl.MarkAllStepsApplied()`
  deleted. `ProposedHunk.IsApplied` is now set only by
  `ProposedFileChangeViewModel.MarkHunksAppliedFromSelection()` right after a successful write, and
  automatically resets to `false` whenever that hunk's `IsSelected` changes or the file's underlying
  diff changes (edit or original-content refresh) — so a step only ever shows "Applied" if its current
  selection was actually the one last written to disk.
- [x] T3.3.3 Conflict detection: if a file changed on disk since the proposal, warn and offer
  re-diff. — done 2026-10-12: `ApplyOrchestrationService.ApplySelected` re-reads each file's current
  disk content at apply-time and compares it to the proposal's `OriginalContent` baseline; a mismatch
  reports `FileApplyOutcome.Conflict` and skips the write rather than overwriting it. The result
  message names the conflicted files and points at the new
  `ProposedFileChangeViewModel.RefreshOriginalContent(workingDirectoryPath)` (re-reads disk and
  re-triggers the existing reactive re-diff) as the way to resolve one.
- [x] T3.3.4 Partial-failure reporting — don't abandon the batch on one failed write. — done
  2026-10-12: `ApplyOrchestrationService.ApplySelected` attempts every selected file independently,
  catching `IOException`/`UnauthorizedAccessException`/path-containment failures per file into
  `FileApplyOutcome.Failed` without stopping the rest of the batch; `ApplyBatchResult` reports
  applied/conflicted/failed counts and file lists, shown via an info dialog after the batch completes.
  Covered by `ApplyOrchestrationServiceTests` and `TutorialViewModelTests`; full solution builds with
  0 warnings/errors, 156/156 tests pass (123 Core + 33 Avalonia).

---

### EPIC 4 — Safe, reversible file updates *(P1)*

**F4.1 — Containment & validation**
- [x] T4.1.1 Canonicalize with `Path.GetFullPath` and reject any target outside the working directory;
  reject absolute and rooted LLM paths. **(Security — do this early.)** — done 2026-10-02 in
  `FileChangeApplyService.Apply`; verified traversal (`../../../etc/passwd`) and absolute-path
  escapes are rejected with `InvalidOperationException`, normal relative paths still write.
- [x] T4.1.2 Reject writes to `.git/`, `bin/`, `obj/`, and anything matched by `.gitignore` unless
  explicitly confirmed. — done 2026-10-13: added `ProtectedPathChecker.IsProtected` (true if any
  path segment before the filename is `.git`/`bin`/`obj`, or `GitIgnoreMatcher.IsIgnored` matches)
  and `GitIgnoreMatcher` (a minimal single-top-level-`.gitignore` matcher - comments, blank lines,
  `!` negation with git's last-match-wins precedence, a leading `/` root anchor, a trailing `/`
  directory marker, and `*`/`**`/`?` wildcards via hand-translated regex; deliberately doesn't
  support `[abc]` character classes, nested per-directory `.gitignore` files, a global gitignore, or
  `.git/info/exclude` - out of scope for an apply-time safety check). `IApplyOrchestrationService.
  Preview` now takes `workingDirectoryPath` and reports `ApplyPreview.ProtectedPathCount`/
  `ProtectedPathFiles` so the confirmation dialog can warn before applying; `ApplySelected` gained an
  `allowProtectedPaths` parameter (default `false`) and `ApplyOne` short-circuits to a new
  `FileApplyOutcome.ProtectedPathBlocked` before touching disk when a target is protected and the
  caller hasn't confirmed, without affecting any other file in the batch (T3.3.4). `TutorialViewModel.
  ApplySelectedAsync` shows a second, separate confirm dialog only when `ProtectedPathCount > 0`;
  declining it only skips those files (reported via `ProtectedPathBlockedCount`) rather than
  aborting the whole apply. Covered by `GitIgnoreMatcherTests`, `ProtectedPathCheckerTests`, new
  `ApplyOrchestrationServiceTests` cases, and new `TutorialViewModelTests` cases (both dialogs
  confirmed/declined independently); full solution builds with 0 warnings/errors, 208/208 tests pass
  (170 Core + 38 Avalonia). `GitIgnoreMatcher.cs` is 92.3% line/100% branch covered - the
  `catch (IOException)`/`catch (UnauthorizedAccessException)` arms around the `.gitignore` read are
  left deliberately untested, same reasoning as the identical gap in `FileEncodingDetector.cs`
  above. `ProtectedPathChecker.cs` and the new model properties are 100% line/100% branch covered.
- [x] T4.1.3 Preserve the file's existing encoding and line endings instead of defaulting. — done
  2026-10-13: `FileChangeApplyService.Apply` always wrote UTF-8 (no BOM), and
  `DiffBuilder.ComposeContent` always joins hunk-composed content with bare `\n` regardless of the
  file's actual convention - so a CRLF or BOM'd file silently flipped to LF/no-BOM on first apply.
  Added `FileEncodingDetector.Detect` (sniffs the on-disk file's BOM - UTF-8/UTF-16 LE/BE/UTF-32
  LE/BE - and dominant newline before the write overwrites it) and `FileEncodingInfo` (the detected
  `Encoding` plus an optional `NewLine`, `null` when there's nothing to preserve - a brand-new file,
  or an existing one with no newlines at all). `Apply` now normalizes `BuildSelectedContent()`'s
  line endings to match and writes with the detected `Encoding` instead of the encoding-less
  `File.WriteAllText` overload. Covered by `FileEncodingDetectorTests` (every BOM variant, CRLF/LF/
  no-newline/mixed, missing file) and new `FileChangeApplyServiceTests` cases (CRLF and LF
  round-trip through hunk composition, BOM round-trip, new-file no-regression); 174/174 tests pass
  (141 Core + 33 Avalonia), solution builds with 0 warnings/errors.
  `FileChangeApplyService.cs` is 100% line/100% branch covered; `FileEncodingDetector.cs` is 92.5%
  line/100% branch — the one gap is the `catch (IOException)` arm, structurally identical to the
  `catch (UnauthorizedAccessException)` arm next to it (both just return `.Default`), left
  deliberately untested rather than forcing a flaky/platform-specific repro of a generic I/O failure
  in a unit test.

**F4.2 — Backup & undo**
- [x] T4.2.1 Snapshot each file before first write in a session-scoped backup store. — done
  2026-10-13: added `IApplyBackupStore`/`ApplyBackupStore` (in-memory, keyed by the proposal's
  relative `FilePath`, registered as a singleton like `IPromptResultStore` so it survives page
  navigation for the app's lifetime). `ApplyOrchestrationService.ApplyOne` now calls
  `SnapshotIfAbsent` with the disk content it already reads for the T3.3.3 conflict check, right
  after `IFileChangeApplyService.Apply` succeeds - so a conflicted, protected-path-blocked, or
  failed attempt never creates a backup entry, and a second apply to a path already written this
  session doesn't overwrite the pre-session baseline with an intermediate state. Doesn't add
  undo/revert or a history UI - that's T4.2.2/T4.2.3. Covered by new `ApplyBackupStoreTests` (store
  in isolation: first-write stores, repeat writes to the same path keep the original, independent
  paths tracked separately, miss returns false) and new `ApplyOrchestrationServiceTests` cases
  (existing-file and new-file first writes snapshot correctly, a second same-session write doesn't
  clobber the snapshot, conflicted/blocked files create none); full solution builds with 0
  warnings/errors, 217/217 tests pass (179 Core + 38 Avalonia). `ApplyBackupStore.cs` is 100%
  line/100% branch covered.
- [x] T4.2.2 "Undo last apply" and "Revert all applied changes in this session". — done
  2026-10-13: added `IApplyHistoryStore`/`ApplyHistoryStore` (session-scoped LIFO stack of
  `AppliedFileChange` batches, singleton like `IApplyBackupStore`) alongside T4.2.1's store so
  "undo last apply" can restore exactly the files one batch touched even when a file's been
  applied more than once this session, while "revert all" still goes back to T4.2.1's session-wide
  baseline via a new `IApplyBackupStore.AllSnapshots`. Extracted `IFileChangeApplyService.WriteRaw`
  out of `Apply` (same path-containment/directory-creation/encoding/line-ending preservation,
  T4.1.3) so both new write-backs reuse it instead of duplicating that logic. `ApplyOrchestrationService`
  gained `TryUndoLastApply`/`RevertAllApplied`, sharing a private `WriteBack` helper that reports
  per-file success/failure via the new `RevertResult`/`RevertFileResult` models; `RevertAllApplied`
  clears the undo history afterward since older batches no longer describe a state worth returning
  to once everything's back at its original content. Wired into the UI as two new
  `TutorialViewModel` commands (`UndoLastApplyCommand`/`RevertAllAppliedCommand`) and buttons,
  following the existing `ApplySelectedCommand` confirm/apply/report-via-status-and-info-dialog
  pattern; a successful revert also calls `ProposedFileChangeViewModel.RefreshOriginalContent` on
  any affected file still in the `Files` collection so the Tutorial/Diffs pages' diff state matches
  disk again. Covered by new `ApplyHistoryStoreTests`, `ApplyBackupStoreTests`/`ApplyOrchestrationServiceTests`
  additions (LIFO undo order, undo/revert restoring the right baseline, conflicted/blocked files
  contributing nothing to a batch, a write-back failure reporting via `FailedCount`/`FailedFiles`),
  direct `FileChangeApplyServiceTests` for `WriteRaw`, and new `TutorialViewModelTests` cases for
  both commands (cancel, nothing-to-undo/revert, success, partial failure, Files refresh); full
  solution builds with 0 warnings/errors, 248/248 tests pass (201 Core + 47 Avalonia).
  `ApplyHistoryStore.cs`, `ApplyBackupStore.cs`, `FileChangeApplyService.cs`, `AppliedFileChange.cs`,
  and `RevertResult.cs` are all 100% line/100% branch covered; `ApplyOrchestrationService.cs`'s one
  remaining gap is the same pre-existing `FileApplyOutcome.Failed` catch arm in `ApplyOne` already
  documented under T4.2.1/T3.3.4, not anything new from this task.
- [x] T4.2.3 Apply-history log surfaced in the UI. — done 2026-10-14: added
  `IApplyHistoryLog`/`ApplyHistoryLog` (session-scoped, append-only list of `ApplyHistoryEntry`
  rows - timestamp, `ApplyHistoryEntryKind`, and the file paths an action actually succeeded on;
  singleton like T4.2.1/T4.2.2's stores), kept deliberately separate from `IApplyHistoryStore`
  (whose batches hold full pre-apply content and get popped away by undo) and `IApplyBackupStore`
  (no concept of individual timestamped actions at all). `ApplyOrchestrationService.ApplySelected`/
  `TryUndoLastApply`/`RevertAllApplied` each record one entry per call, filtered to succeeded files
  only, mirroring the same success-filtering already used for backups and undo batches; an action
  that touches nothing logs nothing. `TutorialViewModel` mirrors the log into a display-ready
  `ObservableCollection<string> ApplyHistoryEntries` (newest first) plus a `HasApplyHistory` flag,
  refreshed after every apply/undo/revert and on `OnNavigatedTo` so navigating away and back doesn't
  show a stale list; `TutorialView.axaml` gained a collapsed-until-first-use history panel (bounded
  `MaxHeight`) in a new third grid row. New `Tutorial_HistoryEntry*` keys added to `Resources.cs`.
  Covered by new `ApplyHistoryLogTests`, `ApplyOrchestrationServiceTests` additions (logs each kind
  with succeeded-only paths, logs nothing when nothing succeeded), and `TutorialViewModelTests`
  additions (`HasApplyHistory` toggling, `OnNavigatedTo` re-reading the log, each command refreshing
  the display newest-first); full solution builds with 0 warnings/errors, 263/263 tests pass (211
  Core + 52 Avalonia). `ApplyHistoryLog.cs` and `ApplyHistoryEntry.cs` are 100% line/100% branch
  covered; `TutorialViewModel.cs`'s only new gap is a defensive `default` arm (`ArgumentOutOfRangeException`)
  in the entry-kind formatting switch, structurally unreachable given the enum is exhaustively
  handled above it; `ApplyOrchestrationService.cs`'s one remaining gap is the same pre-existing
  `ApplyOne` catch arm already documented under T4.2.1/T4.2.2.

**F4.3 — Git integration**
- [x] T4.3.1 Detect a git repo at the working directory; show dirty state before applying. — done
  2026-10-14: added `IGitStatusService`/`GitStatusService`, which shells out to `git status
  --porcelain` (matching `ClaudeCliService`'s convention of treating an external CLI as the source
  of truth rather than taking a LibGit2Sharp-style native dependency) to report whether
  `workingDirectoryPath` is inside a git working tree and which files are dirty there; any failure
  to run git at all (not installed, not a repository, directory missing) is treated the same as
  "not a repository" since this is an informational warning, not a security boundary.
  `ApplyOrchestrationService.Preview` now also takes an `IGitStatusService` dependency and reports
  `ApplyPreview.IsGitRepository`/`GitDirtyFileCount`/`GitDirtyFiles` - scoped the same way
  `ProtectedPathCount`/`Files` already are (T4.1.2): only files among the ones actually selected for
  this apply, not every dirty file in the repository, since an unrelated dirty file elsewhere isn't
  this apply's problem. Registered as `AddTransient<IGitStatusService, GitStatusService>()` in
  `App.axaml.cs`. Covered by new `GitStatusServiceTests` (not-a-repo, missing directory,
  null/whitespace path, clean repo, a modified tracked file, an untracked file - using a real `git
  init`/`commit` in a temp directory, consistent with this test project's no-mocking convention) and
  new `ApplyOrchestrationServiceTests` cases (not-a-repo default, clean repo, a dirty selected file
  reported, a dirty file outside the selected batch not reported). Full solution builds with 0
  warnings/errors, 273/273 tests pass (221 Core + 52 Avalonia - before T4.3.2's additions).
  `GitStatusService.cs` is 89.1% line/80% branch covered - the gap is the `process is null`/
  `catch (Win32Exception or IOException)` arms for a missing `git` executable, the same class of
  deliberately-untested defensive branch as `FileEncodingDetector`'s I/O catch arms (T4.1.3):
  forcing it would mean temporarily hiding `git` from `PATH` mid-test-run, which is flaky and
  platform-specific rather than a real repro.
- [x] T4.3.2 Warn when applying over uncommitted work. — done 2026-10-14: `TutorialViewModel.
  ApplySelectedAsync` now shows a dedicated confirm dialog (new `Tutorial_ApplyGitDirtyConfirm*`
  resource keys) whenever `preview.IsGitRepository && preview.GitDirtyFileCount > 0`, naming the
  affected files. Unlike the T4.1.2 protected-paths confirm, declining here cancels the *whole*
  apply rather than just excluding those files from the batch - overwriting a file that's already
  dirty in git isn't something a per-file skip can make safe (there's no commit to fall back on for
  it either way), so it's a whole-batch decision like the general apply-confirm rather than a
  per-file one like protected paths. Covered by new `TutorialViewModelTests` cases (no dirty files
  skips the extra dialog, confirming both dialogs applies, declining the git-dirty dialog cancels
  without calling `ApplySelected`, declining the general confirm never even asks about dirty files).
  Full solution builds with 0 warnings/errors, 277/277 tests pass (221 Core + 56 Avalonia).
- T4.3.3 Optional: stage applied files, or create a branch/commit per apply batch.

---

### EPIC 5 — Harden the LLM protocol *(P1)*

**F5.1 — Structured output instead of markdown scraping**
- [x] T5.1.1 Move the response contract to JSON (or CLI structured output) with an explicit schema. —
  done 2026-10-16: `PromptViewModel.TutorialInstructionPrompt` now asks for a single JSON object
  (`{"responseType": "answer", "answer": ...}` or `{"responseType": "development", "tutorial": ...,
  "files": [{"path", "content", "steps": [{"title", "explanation", "snippet"}]}]}`) instead of the
  `## Tutorial`/`## Files` markdown headers. New `ClaudeJsonResponse`/`ClaudeJsonFile`/
  `ClaudeJsonStep` (Core/Models) are the wire DTOs; `ClaudeJsonResponseParser.TryParse`
  (Core/Helpers) extracts the first balanced top-level `{...}` object from the raw text via a
  quote/escape-aware brace scan (so a file's `content` string can contain its own braces or
  triple-backtick fences without truncating the object early — the exact fragility class
  `ClaudeResponseParserTests` pinned under the old markdown parser) before deserializing with
  `Newtonsoft.Json` and mapping into the existing `ClaudeParsedResponse`/
  `ProposedFileChangeViewModel`/`ProposedHunk` graph, reusing `HunkLocationResolver` unchanged.
- [x] T5.1.2 Validate against the schema; on failure, retry once with the error fed back. — done
  2026-10-16: `ClaudeJsonResponseParser.TryParse` validates required fields per `responseType`
  (`answer` requires non-empty `answer`; `development` requires non-empty `tutorial`, and every file
  needs a `path`/`content`, every step a `title`) and returns a specific error message rather than
  just pass/fail. `PromptViewModel.RunPromptAsync` now calls the new
  `StructuredResponseParser.Parse` after a successful CLI run; if that had to fall back (see T5.1.3)
  it issues exactly one retry call to `ILlmRunner` with the original prompt plus a new
  `RetryInstructionSuffix` naming the validation error, then re-parses the retry's output as the
  final result. A failed retry call (cancelled, CLI error) keeps the first attempt's
  already-fallback-parsed result rather than discarding it for an error state. Cost/token/duration
  reporting (T6.2.3) sums both calls via a new `CombineUsage`/`SumNullable` helper so a retry's cost
  isn't silently under-reported.
- [x] T5.1.3 Keep the markdown parser as a fallback and cover it with tests. — done 2026-10-16:
  `ClaudeResponseParser` is untouched and still fully covered by `ClaudeResponseParserTests`; new
  `StructuredResponseParser.Parse` is the entry point that prefers `ClaudeJsonResponseParser` and
  only falls through to `ClaudeResponseParser.Parse` (reporting `UsedFallback = true` plus the JSON
  validation error) when the JSON contract couldn't be satisfied even after the T5.1.2 retry — so a
  model that reverts to the old markdown shape, or answers in plain prose, still produces a usable
  result instead of an error. `PromptViewModel` surfaces a `Prompt_FallbackParseNotice` suffix on the
  status message whenever the final result came from the fallback, so the user can tell the response
  wasn't strictly schema-conformant. Known/accepted regression: the live incremental file-reveal
  added in T6.2.2 re-parses the growing `CliOutput` with the markdown parser on every streamed delta
  — harmless but now mostly a no-op under the JSON contract, since a single top-level JSON object
  can't be meaningfully parsed until it's fully closed, so files no longer appear mid-stream the way
  they did under the markdown protocol; the authoritative parse still happens once, at the end, via
  `StructuredResponseParser`. Covered by new `ClaudeJsonResponseParserTests` (both response shapes,
  JSON wrapped in commentary/fences, file content containing its own braces/fences, every validation
  failure path, malformed JSON syntax), `StructuredResponseParserTests` (JSON success, markdown
  fallback, unstructured-text fallback), and new `PromptViewModelTests` cases (invalid-JSON-then-
  corrected-JSON retry succeeding, the retry prompt containing the original request and the
  validation error, both attempts failing and falling back to markdown with the notice shown, a
  failed retry call keeping the first attempt's fallback result, and cost/token summing across a
  retry). Full solution builds with 0 warnings/errors, 332/332 tests pass (251 Core + 81 Avalonia).
  `ClaudeJsonResponseParser.cs` is 97.8% line/89.4% branch covered — the one gap is a defensive
  `parsed is null` check after a successful deserialize, which `Newtonsoft.Json` can only hit for the
  literal JSON text `"null"` that the brace-scanning extractor can never produce (it only ever
  returns a balanced `{...}` object); left deliberately untested rather than forcing a contrived
  repro, same class of gap as other defensive branches noted earlier in this document. Not run
  against the real `claude` CLI — per the user's standing preference (noted under T6.2.3) to avoid
  spending real API usage on verification — so the new prompt's actual compliance rate with a live
  model is unverified; worth a manual smoke test at some point.

**F5.2 — Diffs over the wire, not whole files**
- [x] T5.2.1 Ask for unified diffs / search-replace edit blocks instead of full file contents to cut
  tokens and avoid truncation. — done 2026-10-16: `PromptViewModel.TutorialInstructionPrompt`'s
  `"development"` schema replaces each file's per-step `"content"`/`"steps"` shape with an `"edits"`
  list — `{"title", "explanation", "search", "replace"}` — and tells the model to use it for every
  file that already exists, reserving `"content"` for brand-new files only. New `ClaudeJsonEdit`
  (Core/Models, alongside a new `Edits` list on `ClaudeJsonFile`) is the wire DTO; the two shapes are
  not mutually exclusive in the parser (a file with both is treated as edit-mode, `content` ignored)
  to avoid a hard validation failure for a model that doesn't follow the instruction precisely.
- [x] T5.2.2 Apply those patches locally with fuzz/context matching. — done 2026-10-16: new
  `SearchReplaceEditApplier.Apply` (Core/Helpers) applies a file's edits, in order, against its real
  content — splitting into lines and locating each edit's `search` block with three progressively
  fuzzier strategies (exact; per-line trimmed, for indentation drift; per-line whitespace-collapsed,
  for reformatted internal spacing) before giving up. A `search` that matches more than once under a
  given strategy is treated the same as zero matches — rather than guessing which occurrence was
  meant — since a fuzzier strategy can only ever match a superset of lines, an ambiguous exact match
  stays ambiguous all the way down and the edit is reported unresolved rather than mis-applied.
  `ClaudeJsonResponseParser.TryParse` gained an optional `workingDirectoryPath` parameter (trailing,
  defaults to `null`, so every pre-existing call site is unaffected) and now reads a file's current
  content via the existing `FileContentReader.ReadOrEmpty` and runs the applier *during validation*,
  not mapping — resolving edits requires the real file, so there's no way to validate an edit-mode
  file without doing the apply. `StructuredResponseParser.Parse` grew the same trailing optional
  parameter and `PromptViewModel` now passes its `WorkingDirectoryPath` through on both the initial
  parse and the T5.1.2 retry's. A nice side effect: since the applier already read the file's real
  content to resolve the edits, edit-mode files get `ProposedFileChangeViewModel.OriginalContent` set
  eagerly at parse time instead of waiting for the Diffs/Tutorial pages to lazily read it on
  selection (`RefreshOriginalContent`) — the diff is correct the moment the response is parsed, and
  the later lazy refresh (unchanged) just re-reads the same content.
- [x] T5.2.3 Detect truncated or incomplete responses and refuse to apply them. — done 2026-10-16: an
  edit whose `search` text can't be matched under any of the applier's strategies is treated as a
  **schema validation failure** for the whole response — `"The \"search\" text for edit ... could not
  be matched to that file's current content - it may be incomplete, or the file may have changed
  since the request was made."` — reusing T5.1.2's existing one-shot-retry-with-error-fed-back
  mechanism rather than a parallel "partially apply, flag the rest" path: there's no way to tell a
  step that failed to resolve apart from one that genuinely didn't need to touch the file, so a
  response with even one unresolvable edit is treated as untrustworthy as a whole and is either
  corrected on retry or handed to the markdown fallback (T5.1.3), never partially trusted. A missing
  or blank `WorkingDirectoryPath` is not special-cased — the file is read as empty, which makes any
  non-empty `search` fail to match and surfaces the same validation error, since an edit genuinely
  can't be resolved without knowing which file to resolve it against. Covered by new
  `SearchReplaceEditApplierTests` (exact/multi-line match, multiple edits applied in sequence against
  each other's result, indentation-drift and whitespace-collapse fuzzy fallbacks, not-found, ambiguous
  match, deletion via empty `replace`, null `search`/`replace`, search longer than the file, an
  earlier successful edit surviving a later failed one), new `ClaudeJsonResponseParserTests` cases
  (edit-mode end-to-end against a real temp-directory file including `OriginalContent`, an unmatchable
  edit failing validation, missing `search`/`title`, no working directory, `content`+`edits` both
  present preferring `edits`), a `StructuredResponseParserTests` case threading the working directory
  through, and a new `PromptViewModelTests` case running an edit-mode response end-to-end against a
  real temp working directory via `RunPromptCommand`. Full solution builds with 0 warnings/errors,
  353/353 tests pass (271 Core + 82 Avalonia). `SearchReplaceEditApplier.cs` is 100% line/branch
  covered; `ClaudeJsonResponseParser.cs` is 98.3% line/90.9% branch covered (up from 97.8%/89.4% before
  this task) — the only gap is the same pre-existing defensive `parsed is null` check noted under
  T5.1.3, plus a couple of `?? string.Empty`/`?? []` null-coalescing branches in the content-mode
  mapping path whose null side validation already rules out (e.g. `file.Content` can't be null once
  `TryValidate` has passed) — same class of gap, left untested rather than forced. Not run against the
  real `claude` CLI, same caveat as T5.1.3 — the new `"edits"` instruction's actual compliance rate
  with a live model (especially whether it reliably copies `search` text verbatim) is unverified.

**F5.3 — Prompt template management**
- [x] T5.3.1 Move `TutorialInstructionPrompt` out of `PromptViewModel` into a versioned template file. —
  done 2026-10-16: the instruction text is now `CodeGenToTutorial.Core/Prompts/TutorialInstructionPromptV1.txt`,
  embedded as a resource (`<EmbeddedResource Include="Prompts\*.txt" />` in `.Core`'s csproj) rather
  than a `.csproj`-copied loose file, since Core has no "content root" concept the way the Avalonia
  app's `appsettings.json` does and an embedded resource ships correctly regardless of working
  directory. New `IPromptTemplateProvider`/`PromptTemplateProvider` (Core) reads it via
  `Assembly.GetManifestResourceStream`, caching the content after the first read, and exposes
  `CurrentVersion` (`PromptTemplateProvider.LatestVersion = 1`) so a future schema change can ship as
  a new `TutorialInstructionPromptV2.txt` without mutating the one every past response was parsed
  against - the versioning T5.3.2's settings override will need to pick a version from, not just a
  file-organization nicety. `PromptViewModel` now takes `IPromptTemplateProvider` as a fifth
  constructor parameter and builds `fullPrompt` from `GetTutorialInstructionPrompt() + "\n\n" +
  PromptText`, replacing the old hard-coded `const string TutorialInstructionPrompt`; `RetryInstructionSuffix`
  is untouched (it's a short retry-only message, not the schema contract). Registered
  `IPromptTemplateProvider` as a singleton in `App.axaml.cs`, next to `IPromptResultStore`.
  `PromptViewModelTests.CreateSut()` now constructs a real `PromptTemplateProvider` rather than a fake,
  the same way other tests use real pure/side-effect-free Core services directly; its existing
  `LastPrompt` assertions (`Contains`/`DoesNotContain`) needed no changes since they never depended on
  the exact template text. Added `PromptTemplateProviderTests` (version number, embedded content
  contains both JSON response shapes, repeated calls return identical content). Full solution builds
  with 0 warnings/errors, 356/356 tests pass (274 Core + 82 Avalonia, up from 353 before this task's
  3 new Core tests).
- [x] T5.3.2 Let the user view and override the system preamble in Settings. — done 2026-10-16:
  new `PromptTemplateSettingsKeys.Override` (Avalonia, mirroring `ClaudeCliSettingsKeys`) backs a
  `PromptTemplateOverride` setting - blank/missing means "use the built-in default". `SettingsViewModel`
  now takes `IPromptTemplateProvider` as a third constructor parameter and exposes two things on the
  Settings page: a read-only `DefaultPromptTemplate` (the embedded `TutorialInstructionPromptV1.txt`
  content, so the user has something to view/compare/copy from) and an editable `PromptTemplateOverride`
  that persists via the same `OnNavigatedTo`-restores/`On*Changed`-saves pattern as
  `ClaudeCliExecutablePath`/`ClaudeCliArguments`, plus a `ResetPromptTemplateCommand` (disabled once the
  override is already empty) that clears it. `PromptViewModel.RunPromptAsync` reads
  `PromptTemplateSettingsKeys.Override` directly via its existing `ILocalSettingsService` dependency -
  fresh on every run rather than once in `OnNavigatedTo` - so a change made on the Settings page takes
  effect on the very next run without needing to revisit the Prompt page, same rationale
  `ClaudeCliOptionsProvider` documents for its own settings reads; a blank/whitespace-only override
  falls back to `IPromptTemplateProvider.GetTutorialInstructionPrompt()` exactly like a missing one.
  No changes to `IPromptTemplateProvider`/`PromptTemplateProvider` (Core) were needed - the override
  is purely an Avalonia-layer concern, the same separation `ClaudeCliOptionsProvider` keeps Core's
  `ClaudeCliRunner` free of `ILocalSettingsService`. `SettingsView.axaml` gained an "Override" text box
  (multiline, monospace, matching the Prompt page's CLI-output box styling) with a "Reset to default"
  button, and a read-only "Built-in default" text box below it. Covered by new `SettingsViewModelTests`
  cases (default-matches-provider, override defaults empty, override persists, override restores on
  navigation, reset command's `CanExecute` gating and effect) and new `PromptViewModelTests` cases (no
  override uses the default, a saved override replaces the default, a blank/whitespace override still
  falls back to the default). Full solution builds with 0 warnings/errors, 365/365 tests pass (274 Core
  + 91 Avalonia, up from 356). Not visually verified (no X server in this sandbox, same limitation noted
  on earlier UI-touching tasks) beyond confirming the app still launches without throwing.
- [x] T5.3.3 Named prompt presets (feature / bugfix / refactor / explain) with a tutorial-depth control. —
  done 2026-10-17: new `PromptPreset` (`General, Feature, Bugfix, Refactor, Explain`) and `TutorialDepth`
  (`Standard, Brief, Detailed`) enums (Core `Models/`); `General`/`Standard` are the no-op defaults. Six
  new embedded snippets in `Core/Prompts/` (`PresetFeatureV1.txt`, `PresetBugfixV1.txt`,
  `PresetRefactorV1.txt`, `PresetExplainV1.txt`, `DepthBriefV1.txt`, `DepthDetailedV1.txt`) - one file per
  non-default value rather than a single sectioned file, matching the one-file-per-template precedent
  `TutorialInstructionPromptV1.txt` already set; no csproj change needed since the existing
  `Prompts\*.txt` glob covers them. `IPromptTemplateProvider` gained `GetPresetInstruction(PromptPreset)`
  and `GetDepthInstruction(TutorialDepth)`, both returning `""` for the default value; the single
  `_cachedTemplate` field in `PromptTemplateProvider` became a `Dictionary<string, string>` keyed by
  resource name so each snippet is still read from its embedded resource once and reused. Also edited
  `TutorialInstructionPromptV1.txt` to drop the base preamble's "in detail" wording from the
  `explanation` field description, since that phrase would otherwise conflict with a `Brief` depth
  selection - the depth/preset text is additive, not a replacement, so the base template needed to stop
  asserting a detail level of its own. `PromptViewModel` gained `SelectedPreset`/`SelectedDepth`
  (`[ObservableProperty]`, defaulting to `General`/`Standard`) and `AvailablePresets`/`AvailableDepths`
  (`Enum.GetValues<T>()`) for binding; persisted via new `SavedPromptPreset`/`SavedTutorialDepth` settings
  keys using `Enum.TryParse`/`.ToString()` (not Newtonsoft's default int enum serialization) so a future
  reordering of enum members can't silently change what a saved value means. Preset/depth instructions
  are applied orthogonally to T5.3.2's preamble override - appended after whichever preamble is in effect
  (default or user-overridden) and before the user's own prompt text - since they're extra guidance, not
  schema, so where the preamble came from doesn't matter. `PromptView.axaml` gained two `ComboBox`
  controls ("Preset", "Tutorial detail") next to the existing Timeout control; this is the first
  `ComboBox` usage in the codebase, styled consistently with the page's existing label+control pattern.
  Covered by new `PromptTemplateProviderTests` cases (`General`/`Standard` return empty, each non-default
  value returns distinct non-empty text) and new `PromptViewModelTests` cases (defaults, persistence,
  restoration on `OnNavigatedTo`, `General`+`Standard` add no extra text to the prompt, a selected
  preset/depth's instruction text reaches the LLM runner's prompt). Full solution builds with 0
  warnings/errors, 383/383 tests pass (284 Core + 99 Avalonia, up from 365). Not visually verified (no X
  server in this sandbox, same limitation noted on earlier UI-touching tasks) beyond confirming the app
  still launches without throwing (ran under `timeout`; exit code 124 confirms it was still running, not
  crashed, when killed).

**F5.4 — Context selection**
- [x] T5.4.1 Let the user attach specific files/folders as context for the prompt. — done 2026-10-18:
  new `IFilePickerService`/`FilePickerService` (Avalonia, mirroring `IFolderPickerService`/
  `FolderPickerService`) wraps Avalonia's `OpenFilePickerAsync` with `AllowMultiple = true`, registered
  transient in `App.axaml.cs` next to `IFolderPickerService`; `FakeFilePickerService` added alongside
  `FakeFolderPickerService` for tests. `PromptViewModel` gained `AttachedPaths`
  (`ObservableCollection<string>`, mixed file/folder paths), `SelectedAttachedPath`, and three commands:
  `AddContextFilesCommand` (multi-file picker), `AddContextFolderCommand` (reuses the existing folder
  picker), and `RemoveAttachedPathCommand` (`CanExecute`-gated on a selection, same pattern as
  `ResetPromptTemplateCommand`/`CanResetPromptTemplate`) rather than a per-row remove button, which
  would've needed a non-compiled or ancestor-relative binding from inside the `ListBox`'s item template
  back up to a ViewModel-level command - a selection + one button keeps the XAML in the compiled-binding
  style the rest of the page already uses. The list persists as a `List<string>` under a new
  `SavedContextPaths` settings key - no new persistence plumbing needed since `ILocalSettingsService`
  already round-trips any `T` through Newtonsoft. At run time, `BuildContextFilesBlock` expands each
  attached path - a folder non-recursively to the files directly inside it (not subfolders), a file
  as-is - and embeds successfully-read file contents under an "Additional context files:" header,
  inserted after the preset/depth instructions (T5.3.3) and before the user's own prompt text. Two
  scoping decisions, confirmed with the user before implementation: folders are non-recursive, so
  attaching one can't silently sweep in `.git`/`bin`/`obj`/`node_modules` - the user attaches a subfolder
  explicitly if they want more; and a file that can't be read as text (binary, permissions, deleted since
  attachment) is skipped rather than failing the whole run, since folder attachment in particular can
  easily pick up a non-text file alongside the ones that matter. `PromptView.axaml` gained a "Context
  files" row (Add files.../Add folder.../Remove selected buttons plus a `ListBox` of attached paths)
  between the project-location row and the prompt-text row; all later `Grid.Row` indices shifted down by
  one to make room. Covered by new `PromptViewModelTests` cases (defaults, add/dedupe files, add folder,
  no-op on a cancelled folder pick, remove `CanExecute` gating and effect plus persistence, restoration
  on `OnNavigatedTo`, an attached file's content reaching the prompt, an attached folder including only
  its top-level files and not a nested subfolder's, and no attachments adding no header at all). Full
  solution builds with 0 warnings/errors, 394/394 tests pass (284 Core + 110 Avalonia, up from 383). Not
  visually verified (no X server in this sandbox, same limitation noted on earlier UI-touching tasks)
  beyond confirming the app still launches without throwing (ran under `timeout`; exit code 124 confirms
  it was still running, not crashed, when killed).
- [x] T5.4.2 Show an estimated token count before sending. — done 2026-10-18: new
  `TokenEstimator` (Core `Helpers/`) estimates `text.Length / 4` rounded up - the same rough
  characters-per-token ratio Anthropic's own docs use for English text. This is deliberately a
  ballpark, not a real tokenizer: Anthropic doesn't expose a local one, the CLI only reports the
  real `InputTokens` after a run completes (`LlmRunResult.InputTokens`, shown post-run in
  `UsageSummary`), and a live "as you type" estimate can't afford a round trip to an API endpoint on
  every keystroke anyway. The prompt-assembly logic that used to live inline in `RunPromptAsync`
  (preamble/override, T5.3.2; preset/depth, T5.3.3; context files, T5.4.1; working directory line)
  was extracted into a new `BuildFullPromptAsync()` so the estimate is always computed from the
  exact same string a run would actually send, rather than a second implementation that could drift
  from it. `PromptViewModel` gained `EstimatedTokenCountDisplay`, recomputed via
  `UpdateEstimatedTokenCountDisplay()` whenever anything feeding the prompt changes - `PromptText`,
  `WorkingDirectoryPath` (new `OnWorkingDirectoryPathChanged` partial), `SelectedPreset`/
  `SelectedDepth`, and `AttachedPaths` (hooked via `CollectionChanged` in the constructor, since an
  `ObservableCollection<T>` doesn't go through an `[ObservableProperty]`'s `On*Changed`) - plus once
  more after `OnNavigatedTo` restores all of those from settings. A monotonic `_tokenEstimateVersion`
  counter guards against an older recompute's settings read finishing after a newer one and
  overwriting the display with stale data - a real risk here, unlike most other settings reads in
  this class, since typing triggers a recompute on every keystroke. New `Prompt_EstimatedTokenCount`
  resource string (`"~{0:N0} tokens (estimated)"`, mirroring `Prompt_UsageSummary`'s style).
  `PromptView.axaml`'s "Prompt text" label row gained the estimate right-aligned next to it. Covered
  by new `TokenEstimatorTests` (empty string, rounds up, longer text estimates higher) and new
  `PromptViewModelTests` cases (defaults empty, populated after `OnNavigatedTo`, updates when prompt
  text or attachments change, and matches the exact prompt a run actually sends). Full solution
  builds with 0 warnings/errors, 404/404 tests pass (289 Core + 115 Avalonia, up from 394). Not
  visually verified (no X server in this sandbox, same limitation noted on earlier UI-touching
  tasks) beyond confirming the app still launches without throwing (ran under `timeout`; exit code
  124 confirms it was still running, not crashed, when killed).

---

### EPIC 6 — LLM runner robustness *(P1)*

**F6.1 — Lifecycle control**
- [x] T6.1.1 Cancel button wired to a real `CancellationTokenSource`; kill the child process on
  cancel. — done 2026-10-15: `PromptViewModel` now creates a `CancellationTokenSource` per
  `RunPromptAsync` call, passes its token through to `IClaudeCliService.RunPromptAsync`, and exposes
  a `CancelPromptCommand` (enabled only while a run is in flight) that cancels it. A `Cancel` button
  next to "Send to Claude Code" is visible only while `IsRunning`. Catching `OperationCanceledException`
  around the await reports `Prompt_RunCancelled` ("Cancelled.") instead of falling into the generic
  error handler. Critically, `ClaudeCliService.RunPromptAsync` now wraps `process.WaitForExitAsync` in
  a try/catch for `OperationCanceledException` that calls `process.Kill(entireProcessTree: true)`
  before rethrowing - cancelling the await alone does not stop the child process (or, on Windows, the
  cmd.exe wrapper's own child), so without this the `claude` process would keep running in the
  background after cancel. Covered by new `PromptViewModelTests` cases using a `FakeClaudeCliService.
  CompleteImmediately = false` flag that blocks on `Task.Delay(Timeout.Infinite, cancellationToken)`
  until cancelled, standing in for a real long-running CLI call (cancel while running stops the run
  and reports "Cancelled.", cancel command can't execute when not running, cancel command can't
  execute once a run completes). Full solution builds with 0 warnings/errors, 280/280 tests pass (221
  Core + 59 Avalonia - before T6.1.3's additions). The actual process-kill behavior inside
  `ClaudeCliService.RunPromptAsync` has no direct unit test coverage, since the hardcoded `claude`
  executable can't be substituted for a real long-running process in a test yet - revisit once T6.1.4
  makes the executable path injectable.
- [x] T6.1.2 Configurable timeout with a clear timeout message. — done 2026-10-15: `PromptViewModel`
  now has a `TimeoutSeconds` property (default 300, persisted to local settings under
  `PromptTimeoutSeconds` and restored in `OnNavigatedTo`), exposed on `PromptView` as a
  `NumericUpDown` next to the Cancel button. `RunPromptAsync` runs two independent
  `CancellationTokenSource`s - the existing user-cancel one from T6.1.1, plus a new one constructed
  with `TimeSpan.FromSeconds(TimeoutSeconds)` so it cancels itself once the timeout elapses - linked
  via `CancellationTokenSource.CreateLinkedTokenSource` into the single token actually passed to
  `IClaudeCliService.RunPromptAsync`. Both paths still end up throwing the same
  `OperationCanceledException`, so the catch block distinguishes a timeout from a user cancel by
  checking which of the two underlying sources has `IsCancellationRequested` set, rather than
  guessing from the single linked token - reporting the new `Prompt_RunTimedOut` resource string
  (naming the elapsed seconds and pointing at the Settings page) instead of `Prompt_RunCancelled`.
  Covered by new `PromptViewModelTests` cases (timeout elapsing reports the timeout message and
  still cancels the underlying CLI call via `FakeClaudeCliService.CompleteImmediately = false`;
  `TimeoutSeconds` defaults to 300; changing it persists to settings; `OnNavigatedTo` restores a
  previously saved value) - the timeout test uses `TimeoutSeconds = 0` so the timer fires
  immediately rather than slowing the suite down with a real wait. The existing T6.1.1 cancel tests
  were unaffected since they run well within the 300-second default timeout. Full solution builds
  with 0 warnings/errors, 289/289 tests pass (225 Core + 64 Avalonia). Manually launched the app to
  confirm it starts without any Avalonia binding exceptions after adding the `NumericUpDown`
  binding, but couldn't visually confirm the control's on-screen layout - this sandbox's Wayland
  compositor refuses screenshot capture, so a quick manual look is still worth doing.
- [x] T6.1.3 Preflight check that the CLI exists; actionable error with install guidance if not. —
  done 2026-10-15: added `ExecutablePathResolver.ExistsOnPath`, a helper that searches the `PATH`
  environment variable for an executable the same way the OS shell would resolve a bare command name
  (checking `.exe`/`.cmd`/`.bat` on Windows, since npm installs the `claude` CLI as a `.cmd` shim
  there, and an extension-less exact match on other platforms) - this avoids actually starting a
  process just to check availability. `IClaudeCliService` gained an `IsAvailable()` method;
  `ClaudeCliService.IsAvailable()` delegates to `ExecutablePathResolver.ExistsOnPath("claude")`.
  `PromptViewModel.RunPromptAsync` now checks `IsAvailable()` first and, if false, sets
  `StatusMessage` to the new `Prompt_ClaudeCliNotFound` resource string (which names the install
  command, `npm install -g @anthropic-ai/claude-code`) and returns without starting a run - `IsRunning`
  is never set to true for this path, so the cancel button/progress ring don't appear for a run that
  never started. `ExecutablePathResolver.ExistsOnPath` takes an optional `pathVariable` parameter
  (defaulting to the real `PATH` env var) purely for testability, so tests can point it at a temp
  directory instead of mutating process-wide state. Covered by new `ExecutablePathResolverTests`
  (executable found among several PATH directories, not found, empty PATH, blank PATH entries
  skipped without throwing) and a new `PromptViewModelTests` case
  (`RunPromptAsync_CliNotAvailable_ReportsActionableMessageAndDoesNotRun`) using a new
  `FakeClaudeCliService.AvailableToReturn` flag (defaults to `true` so existing tests are
  unaffected). Full solution builds with 0 warnings/errors, 285/285 tests pass (225 Core + 60
  Avalonia). The Windows-only extension-search branch in `ExecutablePathResolver` is untested on this
  (Linux) development machine - same class of platform-conditional gap as `ClaudeCliService`'s
  existing `cmd.exe` branch.
- [x] T6.1.4 Configurable CLI path/arguments in Settings. — done 2026-10-15: added
  `ClaudeCliOptions` (`ExecutablePath`/`Arguments`, with `ClaudeCliOptions.Default` = `"claude"`/
  `"-p"`) to Core, and threaded an optional `ClaudeCliOptions? options` parameter through
  `IClaudeCliService.RunPromptAsync` and `IsAvailable` (defaulting to `ClaudeCliOptions.Default`
  when not supplied, so T6.1.1-T6.1.3's existing call sites stay correct without change).
  `ClaudeCliService` now builds its `ProcessStartInfo.FileName` from `options.ExecutablePath` and
  splits `options.Arguments` on whitespace into the argument list - naive, no quoting support, but
  sufficient for simple flags like `-p --model=opus`. `ExecutablePathResolver.ExistsOnPath` gained a
  branch so a path containing a directory separator (e.g. an absolute path to a custom build) is
  checked directly with `File.Exists` instead of being searched for on `PATH`, matching how a shell
  treats a command name containing a slash. On the Settings page, `SettingsViewModel` now
  implements `INavigationAware` and exposes `ClaudeCliExecutablePath`/`ClaudeCliArguments`
  (persisted immediately on change, restored in `OnNavigatedTo`, falling back to
  `ClaudeCliOptions.Default` when unset) bound to two new `TextBox`es under a "Claude Code CLI"
  heading. `PromptViewModel.RunPromptAsync` reads those same two settings keys (shared via a new
  `ClaudeCliSettingsKeys` constants class so the Settings and Prompt pages can't drift apart) on
  every run - rather than caching them - so a change made on the Settings page takes effect on the
  very next run without needing to revisit the Prompt page first; the resulting `ClaudeCliOptions`
  feeds both the `IsAvailable` preflight check (T6.1.3) and the actual `RunPromptAsync` call.
  Covered by new `ExecutablePathResolverTests` cases (absolute path to an existing file resolves
  without consulting `PATH`; absolute path to a missing file is `false`), new
  `SettingsViewModelTests` (defaults, persisting a change, restoring a saved value, restoring with
  nothing saved yet - using a new `FakeThemeSelectorService` alongside the existing
  `FakeLocalSettingsService`), and new `PromptViewModelTests` cases (no saved settings passes
  `ClaudeCliOptions.Default` through to the CLI service; saved settings pass the configured
  executable path and arguments through) via a new `FakeClaudeCliService.LastOptions` capture.
  `ClaudeCliService.cs` itself still has no direct test coverage for the actual process-start
  behavior, consistent with the gap already noted under T6.1.1 - it shells out to a real external
  process, so exercising the new argument-splitting/path-selection logic end-to-end isn't something
  a unit test can safely do. Full solution builds with 0 warnings/errors, 298/298 tests pass (227
  Core + 71 Avalonia). Manually launched the app after these changes and confirmed no Avalonia
  binding exceptions on startup; couldn't visually confirm the new Settings page `TextBox`es render
  as intended since this sandbox's Wayland compositor refuses screenshot capture, so a quick manual
  look is worth doing.

**F6.2 — Streaming & progress**
- [x] T6.2.1 Stream stdout into the output pane as it arrives rather than buffering to completion. —
  done 2026-10-15: `ClaudeCliService` now always appends `--output-format stream-json
  --include-partial-messages --verbose` after the user-configured arguments (T6.1.4) - stream-json
  with partial messages is a hard requirement for this service's own parsing to work, not an optional
  CLI choice, so it's unconditional rather than user-configurable. Verified the NDJSON schema for
  free by reading the embedded strings in the installed `claude` CLI binary (`strings -a`) rather
  than spending a real/paid API call: each stdout line is one JSON object; `{"type":"stream_event",
  "event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"..."}}}` carries one
  chunk of assistant text, and a final `{"type":"result",...}` line carries cost/usage/duration
  (T6.2.3). `OutputDataReceived`'s handler now parses each line with `Newtonsoft.Json.Linq.JObject`
  (matching the project's existing `Json.cs` convention) instead of accumulating raw text, and a
  line that fails to parse as JSON is swallowed rather than crashing the run. `IClaudeCliService.
  RunPromptAsync` gained an `Action<string>? onTextDelta` parameter invoked once per text-delta
  chunk, in order, as it arrives - on the process's background output-reading thread, so
  `PromptViewModel` marshals onto the UI thread via `Dispatcher.UIThread.Post` before touching
  `CliOutput`. `ClaudeCliResult.StandardOutput` is now the concatenation of streamed text deltas
  (previously the raw stdout text); for this app's single-turn, no-tool-use prompts that's
  equivalent to the old behavior, just assembled incrementally. Covered by a new
  `ClaudeCliServiceTests` (Core.Tests) that stands a small POSIX shell script in for the `claude`
  binary - the same approach `GitStatusServiceTests` already takes with a real `git` binary -
  asserting deltas are accumulated into `StandardOutput` and that `onTextDelta` is invoked for each
  chunk in order; this closes the test-coverage gap called out under T6.1.1/T6.1.4 (process-start
  behavior had no direct unit test before, since the executable couldn't be substituted). Also a new
  `PromptViewModelTests` case asserting streamed deltas append to `CliOutput` live via
  `FakeClaudeCliService.TextDeltasToEmit`. Known gap: if the CLI ever invokes a tool mid-turn
  (interleaving `tool_use` content blocks with text blocks), deltas are concatenated in arrival order
  regardless of content-block index, which could interleave text from different blocks - not an
  issue today since this app's prompts explicitly ask for a direct text response with no tool use.
- [x] T6.2.2 Parse incrementally so files appear as they are emitted. — done 2026-10-15:
  `PromptViewModel`'s `onTextDelta` callback re-runs `ClaudeResponseParser.Parse` against the
  growing `CliOutput` after every chunk and calls `_promptResultStore.SetResult` with whatever it
  finds - safe to do on every delta because the parser only matches complete, closed-fence `###
  File:` sections (confirmed by reading `ClaudeResponseParser.cs`), so an in-progress file block is
  silently ignored until it closes rather than appearing truncated. This means `DiffsView`/
  `TutorialView` (both bound to the shared `PromptResultStore`) can show a file as soon as its block
  finishes streaming, instead of waiting for the whole CLI run to exit. The existing end-of-run
  `SetResult` call (gated on `ExitCode == 0`) is unchanged and simply re-confirms the same data once
  the run finishes. Covered by a new `PromptViewModelTests` case that streams a complete `## Tutorial`
  /`## Files` response as a single delta chunk and asserts `PromptResultStore.Files` already has the
  file before the run command's `Task` completes (via a new `FakePromptResultStore.OnSetResult` hook
  so the test can observe the intermediate call, not just the final state). Known/accepted gap: if a
  run streams one or more complete file blocks and then the CLI ultimately fails (non-zero exit
  code), those files remain visible in `PromptResultStore` - there's no rollback, since the store has
  no such concept and the approved plan for this task didn't call for one.
- [x] T6.2.3 Elapsed-time and token/cost readout. — done 2026-10-15: `PromptViewModel` starts a
  `Stopwatch` and an Avalonia `DispatcherTimer` (1-second tick) when a run begins, exposing a new
  `ElapsedTimeDisplay` string property (e.g. `"12s"`) bound next to the progress ring on
  `PromptView`, visible only while `IsRunning`. On the Core side, `ClaudeCliResult` gained nullable
  `TotalCostUsd`/`DurationMs`/`InputTokens`/`OutputTokens` fields, populated from the final
  `{"type":"result",...}` NDJSON line's `total_cost_usd`/`duration_ms`/`usage.input_tokens`/
  `usage.output_tokens` fields (null if that line was never seen, e.g. the run was cancelled before
  finishing). `PromptViewModel` turns a successful result with cost data into a new `UsageSummary`
  string (new `Prompt_UsageSummary` resource string: `"Cost: ${0:F4} • {1:N0} in / {2:N0} out tokens
  • {3:F1}s"`), displayed under the status message. Covered by new `ClaudeCliServiceTests` (the
  fake-script `result` line's cost/duration/usage fields land on `ClaudeCliResult` correctly) and new
  `PromptViewModelTests` cases (a result with usage data populates `UsageSummary`; one without leaves
  it empty; `ElapsedTimeDisplay` is set once a run completes). Full solution builds with 0
  warnings/errors, 306/306 tests pass (230 Core + 76 Avalonia). Manually launched the app and
  confirmed no Avalonia binding exceptions after adding the `ElapsedTimeDisplay`/`UsageSummary`
  bindings; couldn't visually confirm their on-screen layout since this sandbox's Wayland compositor
  refuses screenshot capture, so a quick manual look is worth doing. Also worth a real (paid) manual
  smoke test against the actual `claude` CLI at some point - the NDJSON schema here was verified via
  the installed binary's embedded strings rather than a live call, per the user's explicit preference
  to avoid spending real API usage on verification, so it hasn't been exercised end-to-end against
  the real CLI yet.

**F6.3 — Provider abstraction**
- [x] T6.3.1 Abstract behind an `ILlmRunner` so the CLI is one implementation. — done 2026-10-16:
  added `ILlmRunner` (Core) with just `RunPromptAsync(prompt, workingDirectory, onTextDelta,
  cancellationToken)` and `IsAvailableAsync()` - nothing CLI-specific (an executable path, process
  arguments) appears on it, so a future direct Anthropic API runner (T6.3.2) can be a second
  implementation without the interface leaking CLI concepts. Renamed `ClaudeCliService` to
  `ClaudeCliRunner` as the CLI's implementation of it, and replaced `ClaudeCliResult` with a
  provider-agnostic `LlmRunResult` (Core) - `Success`/`Output`/`ErrorMessage` are the fields callers
  should branch on, while `ExitCode` stays as a nullable CLI-only diagnostic a non-CLI runner can
  leave null. The CLI-specific configuration that used to flow in as a per-call `ClaudeCliOptions`
  parameter (T6.1.4) couldn't just move onto the new interface without reintroducing a CLI-shaped
  parameter on it, and Core has no reference to `ILocalSettingsService` (it lives in the Avalonia
  project) to resolve it internally either - so `ClaudeCliRunner` now takes a
  `Func<Task<ClaudeCliOptions>>? optionsProvider` constructor parameter instead (defaulting to
  `ClaudeCliOptions.Default` when not supplied) and calls it fresh on every `RunPromptAsync`/
  `IsAvailableAsync` call. The actual settings-reading that used to live in
  `PromptViewModel.LoadClaudeCliOptionsAsync` moved into a new `ClaudeCliOptionsProvider`
  (Avalonia), which `App.axaml.cs` wires into `ClaudeCliRunner` as that `Func` when registering
  `ILlmRunner` - this is the one seam where Avalonia's settings storage reaches into the otherwise
  settings-agnostic Core runner. `PromptViewModel` now depends on `ILlmRunner` and no longer knows
  about `ClaudeCliOptions`/`ClaudeCliSettingsKeys` at all; `SettingsViewModel` is unaffected since it
  only edits those settings; `IsAvailable` becoming `IsAvailableAsync` (now that it has to resolve
  options via the provider) is the only other signature change callers had to adjust for. Updated
  `ClaudeCliServiceTests` (renamed `ClaudeCliRunnerTests`, now constructing the runner with an
  options-provider lambda instead of passing `options:` per call, plus two new `IsAvailableAsync`
  cases) and `FakeClaudeCliService` (renamed `FakeLlmRunner`, implementing `ILlmRunner` - dropped
  `LastOptions` since the fake no longer receives options at all). The two `PromptViewModelTests`
  cases that asserted settings flow into CLI options (`...UsesDefaultClaudeCliOptions`/
  `...PassesConfiguredExecutablePathAndArguments`) moved to a new `ClaudeCliOptionsProviderTests`,
  since that's `ClaudeCliOptionsProvider`'s responsibility now, not `PromptViewModel`'s. Full
  solution builds with 0 warnings/errors, 308/308 tests pass (232 Core + 76 Avalonia).
- T6.3.2 Add a direct Anthropic API runner (API key in secure storage, model selection).
- T6.3.3 Model picker in Settings.

---

### EPIC 7 — Sessions, history & persistence *(P1)*

**F7.1 — Persist runs**
- [x] T7.1.1 Persist each run (prompt, raw output, parsed result, selections, apply log) to disk.
  — done 2026-10-18: new Core DTOs `RunSession`/`RunSessionFile`/`RunSessionHunk` (Models) capture a
  run's prompt, raw output, tutorial text, working directory, each file's original/proposed/edited
  content and selection state, and the apply log - deliberately excluding `ProposedFileChangeViewModel`'s
  derived `DiffLines`/`DiffHunks`/`MatchedDiffHunks` (always recomputed from the fields that are kept)
  and `ProposedHunk.IsApplied` (resets to `false` on any `OriginalContent` change, so it's meaningless
  after a reload). New `IRunSessionStore`/`RunSessionStore` (Core) exposes `RecordRun(prompt, rawOutput)`
  (starts a new session and writes its first snapshot) and `Resave()` (re-reads the current
  `IPromptResultStore`/`IApplyHistoryLog` state and rewrites the same file; a no-op before any
  `RecordRun`). Each run writes its own file - `{StartedAt:yyyyMMddHHmmssfff}_{Guid:N}.json` under a new
  `Sessions` subfolder of the existing app-data folder - append-only across runs so a later history
  listing (T7.2.x) has every past run available, not just the latest; `RunSessionStore` reads
  `IOptions<LocalSettingsOptions>` the same way `LocalSettingsService` does rather than hardcoding a
  second copy of the app-data folder literal. Internally, `RunSessionStore` tracks the in-progress
  session as a single nullable `ActiveSession` record rather than several loose nullable fields, so
  there's no null-forgiving (`!`) operator anywhere - it's either the one complete session in progress
  or there isn't one yet. `PromptViewModel.RunPromptAsync` calls `RecordRun(fullPrompt, result.Output)`
  once a run reaches a terminal success/failure state (not for a cancelled run or one that threw before
  producing a CLI result) - `fullPrompt` (pre-retry) and `result.Output` (post-retry, since
  `CombineUsage` already folds a retry's output in) so the persisted record matches exactly what was
  sent and what was actually parsed. `ApplyOrchestrationService` - the single Core service that already
  owns every apply/undo/revert mutation - gained a 6th constructor parameter, `IRunSessionStore`, and
  calls `Resave()` after `ApplySelected`/`TryUndoLastApply`/`RevertAllApplied`, each guarded on at least
  one file actually having succeeded (mirroring `RecordBatch`/`Record`'s existing "no-op on an empty
  list" pattern) so a batch that changed nothing doesn't trigger a pointless disk write. Wired into DI in
  `App.axaml.cs` via a factory registration that computes the `Sessions` folder path from
  `IOptions<LocalSettingsOptions>`. Covered by new `RunSessionStoreTests` (10 cases: writes one file per
  recorded run, persists prompt/raw output/tutorial/working directory, persists per-file content and
  hunk selections, falls back to the whole-file selection when a file has no hunks, persists the apply
  log, `Resave` no-ops before any `RecordRun`, `Resave` overwrites the same file rather than creating a
  new one, `Resave` picks up selection and apply-log changes made after `RecordRun`, and calling
  `RecordRun` twice keeps the first file and starts a second), 5 new `ApplyOrchestrationServiceTests`
  cases (resave guarded correctly on an empty apply/undo/revert, resave reflects the new apply-log entry
  after each, and applying before any run was recorded doesn't throw or write a file), and 3 new
  `PromptViewModelTests` cases (a successful run records the prompt actually sent and the raw output, a
  non-zero exit code still records the run, and a run that never starts because the CLI isn't available
  records nothing) using a new hand-written `FakeRunSessionStore`. Full solution builds with 0
  warnings/errors, 422/422 tests pass (304 Core + 118 Avalonia, up from 404).
- [x] T7.1.2 Restore the last session on launch. — done 2026-10-18: `IRunSessionStore` gained
  `RestoreLastSession()`, the inverse of T7.1.1's save path - finds the most recently started
  session file (same sortable-filename trick `RecordRun` writes, so no need to open every file to
  compare `StartedAt`), rebuilds each file's `ProposedFileChangeViewModel`/`ProposedHunk` graph
  (content, edits, and hunk/whole-file selection state) from its `RunSessionFile`/`RunSessionHunk`
  snapshot, and pushes the result into `IPromptResultStore` via the existing `SetResult` plus a new
  `IApplyHistoryLog.Restore(entries)` (replaces the log wholesale, preserving original timestamps -
  distinct from `Record`, which always stamps `UtcNow` for a new action). Also re-adopts the loaded
  session as `RunSessionStore`'s active one, so a `Resave()` after relaunch keeps overwriting that
  same file instead of orphaning it. Deliberately out of scope, matching what T7.1.1 chose not to
  persist in the first place: `IApplyHistoryStore`'s undo stack and `IApplyBackupStore`'s session
  baseline, both of which only ever held in-memory full-file content for the running session and
  were never written to disk - "Undo Last Apply" naturally has nothing to undo right after a
  restart, same as before this task. Wired into `App.axaml.cs`'s `InitializeThemeAndNavigate`,
  before navigating to `MainViewModel`, so the Tutorial/Diffs pages already have last session's
  content the first time the user opens them - no `MainViewModel`/`MainView` changes needed. Covered
  by 7 new `RunSessionStoreTests` cases (no sessions folder returns null and touches neither store,
  an empty folder returns null, restoring picks the most recently recorded session, file content and
  hunk selection round-trip, the no-hunks whole-file fallback round-trips, the apply log round-trips,
  and restoring then resaving overwrites the restored file rather than creating a new one) and 2 new
  `ApplyHistoryLogTests` cases for `Restore`. Full solution builds with 0 warnings/errors, 431/431
  tests pass (313 Core + 118 Avalonia, up from 422).

**F7.2 — History UI**
- [x] T7.2.1 History page listing past runs with timestamp, prompt excerpt, and file count. — done
  2026-10-18: new `IRunSessionStore.ListSessions()` returns every recorded session newest-first
  (shares a `GetSessionFileNamesNewestFirst()` helper with `RestoreLastSession`, which now also
  gets a monotonic sequence number in the session filename - `RecordRun`'s previous guid-only
  tiebreak sorted same-millisecond runs randomly instead of by call order, which was previously
  masked by luck but surfaced as a real, reproducible test flake once `ListSessions` made ordering
  observable from two runs recorded back-to-back). New `HistoryViewModel`/`HistoryView` (registered
  in `PageService`/`App.axaml.cs`, added as a "History" item in `ShellView`'s `NavigationView`) list
  each run as a `HistorySessionRow` (`StartedAt`/excerpted `UserPromptText`/`FileCount`, pre-formatted
  into one display string via a new `History_SessionRow` resource key, following
  `TutorialViewModel.FormatHistoryEntry`'s precedent over XAML `StringFormat`).
- [x] T7.2.2 Reopen a past run's tutorial and diffs read-only. — done 2026-10-18: selecting a session
  rebuilds its `RunSessionFile`s into `ProposedFileChangeViewModel`s via a new shared
  `Paarde.Core.Helpers.RunSessionFileConverter` (the `ToRunSessionFile`/`ToProposedFileChangeViewModel`
  conversions that used to be private to `RunSessionStore`, now also used by `HistoryViewModel`) and
  lists them in a second master list; picking a file shows it in the existing `DiffsDetailControl`
  unmodified - it only renders `DiffLines`, which `ProposedFileChangeViewModel` already computes from
  the restored `OriginalContent`/`EditedContent` on construction, so no disk read of the file's
  *current* state is needed or wanted for a historical run.
- [x] T7.2.3 Re-run or fork a past prompt. — done 2026-10-18: `RunSession` gained `UserPromptText`
  (defaults to `""` for pre-existing session files) alongside the existing `Prompt`, since `Prompt` is
  the fully composed text actually sent to the CLI (instruction template + preset/depth + context
  files + the user's text) - resending that as-is would double up the instruction template. New
  `PromptLaunchRequest(PromptText, WorkingDirectoryPath, AutoRun)` navigation parameter, handled in
  `PromptViewModel.OnNavigatedTo`: loads `UserPromptText`/`WorkingDirectoryPath` into the page, then
  (`AutoRun: true`, "Re-run") immediately invokes `RunPromptCommand` the same way the button would, or
  (`AutoRun: false`, "Fork") leaves it for the user to edit first. Both go through the *current*
  instruction template/preset/depth/context-files pipeline rather than literally replaying the old
  composed prompt. `IRunSessionStore.RecordRun` widened to `(prompt, rawOutput, userPromptText)`.
  Covered by new `RunSessionStoreTests`/`RunSessionFileConverterTests`/`HistoryViewModelTests`/
  `PromptViewModelTests` cases. Full solution builds with 0 warnings/errors, 450/450 tests pass (322
  Core + 128 Avalonia, up from 431).

**F7.3 — Multi-turn conversation**
- [x] T7.3.1 Follow-up prompts that carry the prior turn's context (CLI session resume or API
  history). — done 2026-10-18: chose the CLI's `-r/--resume <session-id>` over `-c/--continue`
  because `--continue` can't target a specific prior turn (it always resumes "the most recent
  conversation in cwd", which breaks as soon as a user runs something unrelated in between) while
  `--resume` needs only a session id this process already has. `LlmRunResult` gained `SessionId`,
  captured in `ClaudeCliRunner` from the stream-json `"result"` line's `session_id` field (the same
  line `TotalCostUsd`/`DurationMs`/usage already come from); `ILlmRunner.RunPromptAsync` gained a
  `resumeSessionId` parameter that `ClaudeCliRunner` turns into `--resume <id>` ahead of the fixed
  streaming flags. `PromptViewModel` tracks the last run's session id in-memory only (not persisted
  to `RunSession`/disk - scoped to the current app session, same precedent as T7.1.2's undo stack)
  behind a new "Continue previous conversation" checkbox (`ContinuePreviousConversation`, gated by
  `HasPreviousSession` so it can't be checked before a session id exists); a schema-validation retry
  resumes from the same id as its original attempt since both are the same logical turn. Changing
  `WorkingDirectoryPath` clears the tracked session id and unchecks the box, since a CLI session is
  scoped to the directory it ran in. Covered by new `ClaudeCliRunnerTests` (argv echoing fake script
  asserts `--resume` is/isn't passed; session_id capture) and `PromptViewModelTests` cases. Full
  solution builds with 0 warnings/errors, 456/456 tests pass (324 Core + 132 Avalonia, up from 450).
- [x] T7.3.2 "Refine this file" / "explain this step further" inline actions that issue a scoped
  follow-up. — done 2026-10-18: both actions resume the session id tracked in `IPromptResultStore`
  (widened with a `SessionId` property, set only on a successful run - the mid-stream partial-result
  `SetResult` call passes `null` so it never clobbers the tracked value) rather than starting a fresh
  conversation, since both pages' `ProposedFileChangeViewModel`s originate from that same run.
  "Explain this step further" (`TutorialStepViewModel.ExplainFurtherCommand`) asks for plain prose on
  a resumed session that was told to always answer in JSON; a model that answers in JSON anyway is
  unwrapped via `ClaudeJsonResponseParser.TryParse` (`.Tutorial` doubles as the prose text for an
  "answer" response) rather than adding a second parser. "Refine this file"
  (`TutorialViewModel.RefineSelectedFileCommand`) reuses the main run's exact instruction
  template/JSON contract and `StructuredResponseParser`, scoped with a "only modify this file"
  instruction plus the file's current content; on success the matching file in the response becomes
  a new `ProposedFileChangeViewModel` (immutable by design - `Hunks` is init-only, `ProposedContent`
  has no setter) swapped by index into both `TutorialViewModel.Files` and
  `IPromptResultStore.Files`, keeping the Diffs page in sync without new pub/sub wiring. Deliberately
  no schema-validation retry loop for either action (unlike the main run's one-shot retry) - a failed
  attempt just shows an error and the user clicks again - and no "Refine this file" UI on the Diffs
  page (Tutorial page only, since that's where the apply/edit workflow lives). Covered by new
  `TutorialStepViewModelTests`, new `TutorialViewModel` refine cases, and `PromptResultStoreTests`/
  `PromptViewModelTests` cases for the new `SessionId` field. Full solution builds with 0
  warnings/errors, 471/471 tests pass (326 Core + 145 Avalonia, up from 456).
- T7.3.3 Ask-a-question-about-this-change affordance inside the tutorial.

---

### EPIC 8 — Make it feel like an IDE *(P1/P2)*

**F8.1 — Project explorer**
- [x] T8.1.1 File-tree view of the working directory with gitignore-aware filtering. — done
  2026-10-19: new "Explorer" page (`ExplorerViewModel`/`ExplorerView`, added to `PageService` and
  `ShellView`'s nav menu) shows a `TreeView` of the working directory last picked on the Prompt
  page - read from the same persisted `SavedWorkingDirectory` setting (now a shared
  `WorkingDirectorySettingsKeys` constant, mirroring `ClaudeCliSettingsKeys`) rather than
  `IPromptResultStore.WorkingDirectoryPath`, so the tree is browsable as soon as a folder is
  picked, not only after a prompt has actually run. A new `FileTreeBuilder` (`Paarde.Core.Helpers`)
  walks the directory into an immutable `FileTreeEntry` tree (directories before files,
  alphabetical), always excluding `.git`, and skipping anything the project's root `.gitignore`
  matches via the existing `GitIgnoreMatcher` (T4.1.2) - reusing it rather than writing a second
  ignore-matching implementation means "ignored" means the same thing here as it does for
  `ProtectedPathChecker`'s apply-time safety check. That matcher was previously documented as not
  supporting `[...]` character classes; it now does (additive, regex `[...]` passthrough), since
  this very repo's own `.gitignore` relies on exactly that (`[Bb]in/`, `[Oo]bj/`) and the feature
  would otherwise fail to filter build output in its own dogfood case. Nested per-directory
  `.gitignore` files are deliberately still out of scope, matching that matcher's existing
  documented boundary - the root `.gitignore` covers the common case this task asks for. The
  `TreeView`'s hierarchy comes from Avalonia's `TreeDataTemplate` (`ItemsSource="{Binding
  Children}"`), not a sub-page per node, so expand/collapse state is just the control's own,
  with no new ViewModel-level state to track. Covered by new `GitIgnoreMatcherTests` (character
  class cases), `FileTreeBuilderTests`, and `ExplorerViewModelTests`. A full interactive
  screenshot verification was attempted via headless rendering but the `dotnet run` build of the
  scratch verification project hung without producing output after several minutes - abandoned per
  the same "don't rabbit-hole on an unrelated environment limitation" judgment as T8.4.5, relying
  instead on the new tests plus a clean compiled-bindings build (which validates every binding
  here, including `TreeDataTemplate`'s, at build time). Full solution builds with 0 warnings/
  errors, 520/520 tests pass (355 Core + 165 Avalonia).
- [x] T8.1.2 Badges marking files touched by the current proposal. — done 2026-10-19: new
  `FileTreeNodeViewModel` (`Paarde.Avalonia.ViewModels`) wraps each `FileTreeEntry` with an
  `IsTouched` flag - kept Avalonia-side rather than on `FileTreeEntry` itself since "touched by
  the current proposal" is UI/session state, not something inherent to a file on disk.
  `ExplorerViewModel` now also takes `IPromptResultStore` (the same singleton `DiffsViewModel`
  already reads) and, on each tree build, resolves every `ProposedFileChangeViewModel.FilePath`
  (relative) to an absolute path the same way `ProposedFileChangeViewModel.RefreshOriginalContent`
  does (`Path.Combine(workingDirectoryPath, FilePath)`, then `Path.GetFullPath`) and matches it
  against each `FileTreeEntry.FullPath` with `StringComparison.Ordinal` - the one existing
  path-comparison precedent in the codebase (`FilePathContainment`). `ExplorerView.axaml`'s
  `TreeDataTemplate` switched from `FileTreeEntry` to `FileTreeNodeViewModel` and now shows a
  small accent-colored dot next to a touched file's name. Covered by new `ExplorerViewModelTests`
  cases: a touched file, an untouched sibling, no proposal at all, and a touched file nested in a
  subdirectory. Full solution builds with 0 warnings/errors, 523/523 tests pass (355 Core + 168
  Avalonia).
- [x] T8.1.3 Open any file in the editor, proposed or not. — done 2026-10-19: `ExplorerViewModel`
  gained `SelectedNode`, `SelectedFilePath`, and `SelectedFileContent`; selecting a file node reads
  its content via the existing `FileContentReader.ReadOrEmpty` (same helper
  `ProposedFileChangeViewModel.RefreshOriginalContent` and `ApplyOrchestrationService` already use),
  while selecting a directory or clearing the selection clears the preview. `RefreshTree()` also
  resets `SelectedNode` first, since rebuilding `Root` replaces the whole node tree and would
  otherwise leave the preview pointing at a stale, orphaned node. `ExplorerView.axaml` is now a
  split view: the existing `TreeView` (now with `SelectedItem` bound two-way to `SelectedNode`) on
  the left, a read-only `CodeEditor` (the same control `TutorialDetailControl` uses for editable
  proposal content, here with `IsReadOnly="True"` and bound one-way) on the right, with a
  placeholder message when nothing is selected. Deliberately not wired to
  `ProposedFileChangeViewModel.EditedContent` even for touched files - Explorer stays a browse/view
  surface, editing a proposal stays the Tutorial page's job. Covered by new
  `ExplorerViewModelTests` cases: selecting a file loads its content, selecting a directory clears
  the preview, selecting null clears it, and refreshing the tree clears a stale selection.
  Also fixed two pre-existing bugs in `CodeEditor` surfaced by this page, found via
  `Avalonia.Headless` pixel-capture probes (`CaptureRenderedFrame`) since compiled-binding
  validation and property-level checks both passed while the control still rendered as blank
  white pixels - standard unit tests can't catch this class of bug. The actual root cause: `CodeEditor`
  subclasses `TextEditor` (a `TemplatedControl`) without overriding `StyleKeyOverride`, which defaults
  to `GetType()`; Avalonia therefore looked for a `ControlTheme` keyed `{x:Type CodeEditor}`, found
  none, and applied no template at all - no background Border, no `TextView`, nothing rendered,
  regardless of what `Text`/`Background`/`Foreground` were set to. Fixed by overriding
  `StyleKeyOverride` to return `typeof(TextEditor)` so `CodeEditor` reuses `TextEditor`'s theme.
  Separately (and still worth keeping): AvaloniaEdit.TextMate only recolors syntax tokens, it never
  sets the editor's own `Background`/`Foreground`, so once the template fix made the control visible,
  the DarkPlus theme's light foreground text would otherwise be drawn on whatever background the
  app's current Fluent theme defaulted to - invisible in Light theme. `CodeEditor` now reads
  `editor.background` / `editor.foreground` from `Installation.TryGetThemeColor` and applies them
  directly after `InstallTextMate` returns, since `Installation`'s constructor already fires its one
  and only `AppliedTheme` event before any subscriber (including a handler added right after
  `InstallTextMate`) could ever receive it. Both fixes confirmed visually via a headless-rendered PNG
  showing correctly colored, line-numbered, syntax-highlighted text. Full solution builds with 0
  warnings/errors, 527/527 tests pass (355 Core + 172 Avalonia).

**F8.2 — Shell & workspace**
- [x] T8.2.1 Replace the empty `MainView` with a real dashboard/landing page (recent projects, recent
  runs, quick prompt). — partial progress 2026-10-18: `MainView` is no longer an empty `Grid` - it
  now shows a logo/wordmark header (a plain vector monogram badge; no image-generation tooling was
  available to produce real artwork) and a static numbered "Getting started" walkthrough of the
  Prompt → Tutorial/Diffs → Apply → Settings flow, written directly in `MainView.axaml` with no
  `MainViewModel` changes needed, matching the existing convention that markup-only strings live in
  `.axaml` rather than `Strings/Resources.cs`. Recent projects, recent runs, and a quick-prompt
  entry point are still outstanding - this task isn't done, just no longer blocked on there being
  nothing on the page at all.
  Done 2026-10-19: `MainViewModel` gained `IRunSessionStore`/`ILocalSettingsService`/
  `INavigationService` dependencies (all already-registered singletons) and now implements
  `INavigationAware`. `OnNavigatedTo` reads `IRunSessionStore.ListSessions()` (the same source the
  History page uses) and populates `RecentRuns` (newest 5, reusing `HistorySessionRow.From` for
  identical display formatting) and `RecentProjects` (newest 5 *distinct* `WorkingDirectoryPath`
  values - `Distinct()` over an already-newest-first list keeps each path's most recent occurrence
  without a second sort). Both lists are selection-driven (`SelectedRecentRun`/
  `SelectedRecentProject` + an `Open`/`Switch to this project` button) rather than a command bound
  per row reaching back out to `MainViewModel` from inside an `ItemsControl`'s `DataTemplate` -
  mirrors `HistoryViewModel.Selected`/`ForkCommand` exactly, and sidesteps any ancestor-binding
  fragility under compiled bindings. `OpenRecentRunCommand` reopens a run on the Prompt page via a
  `PromptLaunchRequest` with `AutoRun: false` (same as `HistoryViewModel.Fork` - this is the app's
  first screen, so sending straight to the CLI without a chance to review first would be
  surprising). `OpenRecentProjectCommand` is deliberately *not* a `PromptLaunchRequest` - it only
  saves the chosen path under `WorkingDirectorySettingsKeys.WorkingDirectory` (the same setting
  `PromptViewModel`/`ExplorerViewModel` already read) and navigates with no parameter, so it
  doesn't clobber whatever draft prompt text is already saved there the way overriding
  `PromptText` would. The quick-prompt box (`QuickPromptText` + `SendQuickPromptCommand`, gated on
  non-blank text) also hands off via `PromptLaunchRequest` with `AutoRun: false`, reading the
  last-saved working directory first so the Prompt page doesn't lose it the way passing `""` would.
  Covered by new `MainViewModelTests` (empty state, population/cap/distinct-projects from
  `ListSessions`, both commands' `CanExecute` gating and navigation parameters, quick-prompt gating
  and navigation). Full solution builds with 0 warnings/errors, 536/536 tests pass (355 Core + 181
  Avalonia). Not visually verified - this sandbox has no X server and no committed
  `Avalonia.Headless` test host (prior attempts at this, e.g. T8.1.1/T8.4.5, were one-off scratch
  probes, not infrastructure left in the repo) - reviewed by hand and via the new tests instead.
- [x] T8.2.2 Tabbed editing of multiple files. — done 2026-10-19: the Tutorial page's detail pane
  previously showed only `Selected` via a single `ContentControl`, throwing away and rebuilding
  `TutorialDetailControl` on every selection change - so switching back to a file you'd already
  edited lost nothing (the edit lives on the `ProposedFileChangeViewModel` itself), but there was
  no way to see more than one file's editor at a time or jump directly back to one without
  re-picking it from the master list. `TutorialViewModel` gained an `OpenTabs`
  `ObservableCollection<ProposedFileChangeViewModel>` - a subset of `Files`, populated lazily as the
  user selects files rather than all of `Files` up front, so the page opens with just the one file
  `EnsureItemSelected` already auto-selects as its single tab. `OnSelectedChanged` now adds `value`
  to `OpenTabs` if it isn't already there (a no-op once a file's tab is already open, including the
  jump-from-Diffs and refine-this-file paths, which both just set `Selected`); `ReplaceFile` swaps a
  refined file into its old tab's position in `OpenTabs` the same way it already does for `Files`
  and `_promptResultStore.Files`, so refining a file never leaves a stale duplicate tab behind.
  Closing a tab is a new `CloseSelectedTabCommand` (gated on `Selected is not null`) acting on
  `Selected` rather than a per-tab close button bound back to the view model - mirrors the
  `Selected`+action-button pattern this codebase already uses throughout (e.g.
  `HistoryViewModel.Selected`/`ForkCommand`, and T8.2.1's `MainViewModel`) instead of introducing
  this repo's first per-row/per-tab `DataTemplate` command binding back to a parent `DataContext`.
  Closing a tab selects a neighboring one (`OpenTabs[Math.Min(index, OpenTabs.Count - 1)]`) so the
  detail pane stays populated while other tabs remain open, or `null` if that was the last tab -
  deliberately doesn't auto-reopen anything from `Files` afterward, respecting the explicit close.
  `OpenTabs` resets on every `OnNavigatedTo` alongside `Files`/`Selected`, which is safe rather than
  a regression: `TutorialViewModel` is `AddTransient`, so a fresh instance (and therefore empty
  `OpenTabs`) is already resolved on every navigation to this page today, independent of this
  change. `TutorialView.axaml` replaces the old `ContentControl` with a `Grid` (shown only when
  `HasOpenTabs`) containing a "Close Tab" button bound to `CloseSelectedTabCommand` and a
  `TabControl` (`ItemsSource="{Binding OpenTabs}"`, `SelectedItem="{Binding Selected, Mode=TwoWay}"`)
  whose tab headers show each file's trimmed path plus a `*` when `IsDirty`, and whose content
  template still hosts `TutorialDetailControl` per tab - built with Avalonia's own `TabControl`, no
  new xmlns import needed. Covered by new `TutorialViewModelTests`: auto-selected file opens as the
  lone initial tab, selecting another file adds an additional tab without closing others,
  reselecting an already-open tab doesn't duplicate it, `CloseSelectedTabCommand` `CanExecute`
  gating and closing a non-last tab selects its neighbor, closing the last tab clears `Selected`,
  and refining a file swaps it into the same tab position rather than appending a new one. Full
  solution builds with 0 warnings/errors, 543/543 tests pass (355 Core + 188 Avalonia). Not visually
  verified - same sandbox limitation as T8.2.1 and every other UI-only task (no X server, no
  committed `Avalonia.Headless` test host) - reviewed by hand and via the new tests instead.
- [x] T8.2.3 Resizable/collapsible panes; persist layout. — done 2026-10-19: every master-detail
  page's browse-list pane (Explorer's tree, Diffs' and Tutorial's file list, History's sessions
  and files lists) was a fixed-pixel-width `Grid` column with no way to resize it (Explorer alone
  already had a `GridSplitter`, but it didn't remember a drag between visits). A new
  `ResizablePaneHelper` (`Paarde.Avalonia/Helpers`) wraps one `ColumnDefinition` + `ILocalSettingsService`:
  `RestoreAsync` (called from each View's `Loaded` handler) reads a persisted width back in, falling
  back to that page's original fixed width if nothing's been saved yet; `OnSplitterDragCompleted`
  (wired to the matching `GridSplitter.DragCompleted` - inherited from `Thumb`, fired once per drag
  rather than on every `DragDelta`) saves the column's current width back out. "Collapsible" is
  deliberately *not* a separate collapsed flag/toggle button - dragging a splitter all the way to one
  side already produces (and persists) a width of 0, and dragging it back out restores the pane,
  which is simpler than keeping a second piece of state in sync with the dragged width and avoids
  this being the app's first per-page custom chrome bolted outside the resizable area just to
  reopen it. Explorer, Diffs, and Tutorial each needed their `ColumnDefinitions="280,*"`\-style
  shorthand expanded to an explicit `<Grid.ColumnDefinitions>` block (needed to reach a specific
  `ColumnDefinition` by index from code-behind) plus a new `GridSplitter` column between the master
  and detail panes (Diffs and Tutorial didn't have one at all before this). History got two -
  sessions|files and files|detail - since it has two list panes in a row. Settings keys live in a
  new `WorkspaceLayoutSettingsKeys` (mirrors `ClaudeCliSettingsKeys`/`WorkingDirectorySettingsKeys`),
  one per page+pane so they can't collide. `ResizablePaneHelper` itself only touches a
  `ColumnDefinition` and `ILocalSettingsService`, so (unlike the View/XAML wiring around it) it's
  unit-testable without an Avalonia application/window - covered by new `ResizablePaneHelperTests`:
  default width with nothing persisted, restoring a previously-saved width, clamping a corrupt
  negative persisted width to 0, and persisting on drag-completed including the all-the-way-shut
  (0-width) case. Full solution builds with 0 warnings/errors, 548/548 tests pass (355 Core + 193
  Avalonia). Not visually verified - same sandbox limitation as every other UI-only task (no X
  server, no committed `Avalonia.Headless` test host) - reviewed by hand and via the new tests
  instead.
- [x] T8.2.4 **Recent-projects list and fast project switching.** — done 2026-10-20: T8.2.1 already
  put a Recent Projects list on the Main page, but that only helps when landing on that page - it
  doesn't help while already working on the Prompt page. This adds a `RecentWorkingDirectories`
  collection + `HasRecentWorkingDirectories`/`SelectedRecentWorkingDirectory` to `PromptViewModel`,
  populated on `OnNavigatedTo` the same way (`IRunSessionStore.ListSessions()` -> distinct
  `WorkingDirectoryPath`, newest-first, capped at 5) as `MainViewModel.RecentProjects`, and a
  `Recent...` `ComboBox` next to the existing **Browse...** button in `PromptView.axaml` (only
  visible once there's at least one entry). Picking an item immediately sets `WorkingDirectoryPath`,
  persists it via the same `WorkingDirectorySettingsKeys.WorkingDirectory` setting
  `BrowseWorkingDirectoryAsync` already writes, and resets the selection back to null so the
  dropdown returns to its placeholder rather than looking permanently stuck on one entry - the
  working directory text box above it already shows the current value, so the combo doesn't need
  to keep "holding" it. No new service or command was needed; this is deliberately the same
  shape as `MainViewModel`'s existing recent-projects handling rather than a new abstraction.
  Covered by 4 new `PromptViewModelTests`: no sessions leaves the list empty, recent working
  directories come back distinct and capped at 5, and selecting an entry switches/persists the
  working directory and clears the selection. Full solution builds with 0 warnings/errors,
  551/551 tests pass (355 Core + 196 Avalonia). Not visually verified - same sandbox limitation as
  every other UI-only task (no X server, no committed `Avalonia.Headless` test host).

**F8.3 — Better diff viewer**
- [x] T8.3.1 **Side-by-side mode in addition to inline.** — done 2026-10-20: a new
  `DiffBuilder.BuildSideBySideRows(IReadOnlyList<DiffLine>)` (Core, alongside `BuildLineDiff`/
  `GroupIntoHunks`) turns the existing flat line-level diff into `SideBySideDiffRow`s - pairing up
  each maximal run of consecutive Removed/Added lines index-by-index (preserving each side's own
  order) so an old/new line pair lands on the same row, padding the shorter side with a blank cell
  when a run has more lines on one side than the other; an Unchanged line gets its own row with
  identical text on both sides. `DiffsDetailControl` (shared by the Diffs page, History page's file
  view, and the Diffs jump-to-hunk flow) gained a `Side-by-side` `ToggleButton` next to its existing
  `Copy file contents` button and a second `ItemsControl`, hidden by default, templated over
  `SideBySideDiffRow` as two columns with a thin divider. This is deliberately local view state,
  not persisted and not added to the shared `ProposedFileChangeViewModel` (which both the Tutorial
  and Diffs pages bind to the same instance of) - the view mode is a property of this one control,
  not of the file being shown. `SideBySideItemsControl.ItemsSource` is set from code-behind
  (recomputed on `DataContextChanged`, i.e. switching files, and on the bound file's own
  `DiffLines` changing, i.e. an edit) rather than bound in XAML, since `BuildSideBySideRows`'s
  output isn't something `ProposedFileChangeViewModel` itself exposes. T8.4.5's scroll-to-hunk jump
  (`ApplyPendingScrollTarget`) always switches back to inline first and resets the toggle - the
  side-by-side rows have no equivalent index-based scroll target, and landing in the wrong mode
  after a deliberate jump would be confusing. Covered by 8 new `DiffBuilderTests` (no changes pairs
  every unchanged line with itself, equal-count removed/added pairs one-to-one, more-added and
  more-removed cases pad the shorter side with blank rows, a change sandwiched between unchanged
  lines keeps those on their own rows, an empty diff returns no rows, and prefix text is blank only
  for a padded cell). Full solution builds with 0 warnings/errors, 558/558 tests pass (362 Core +
  196 Avalonia). Not visually verified - same sandbox limitation as every other UI-only task (no X
  server, no committed `Avalonia.Headless` test host).
- T8.3.2 Collapse unchanged regions with expand-context controls.
- T8.3.3 Intra-line (word-level) highlighting.
- T8.3.4 Syntax highlighting inside the diff; next/previous-change navigation.

**F8.4 — Tutorial presentation**
- [x] T8.4.1 **Display the `## Tutorial` overview that is currently parsed and thrown away.** — done
  2026-10-18: `TutorialViewModel` gained a `Tutorial` property (pulled from `PromptResultStore.Tutorial`
  on `OnNavigatedTo`, same pull-on-navigation pattern as `Files`) and `HasTutorialOverview`;
  `TutorialView.axaml` shows it as an "Overview" header above the per-file detail pane, collapsed
  entirely when empty/whitespace-only.
- [x] T8.4.2 Render markdown in explanations (code spans, lists, emphasis) instead of flat text. —
  done 2026-10-18: new `MarkdownInlineParser` (`Paarde.Core.Helpers`) parses the limited subset the
  LLM's tutorial/explanation text actually uses - bullet/numbered lists, `**bold**`/`*italic*`
  emphasis, and `` `code` `` spans - into `MarkdownLine`/`MarkdownSpan` records, deliberately scoped
  to single-line constructs (no headings, fenced blocks, tables, or nested lists); full markdown
  rendering was rejected as unnecessary weight for what the model's responses actually contain. A
  new `MarkdownTextBlock` control (`Paarde.Avalonia.Controls`) renders that output as one wrapped
  `TextBlock` per line - bulleted/indented for list items, with `Run`s styled per span (bold
  `FontWeight`, italic `FontStyle`, code in a monospace font with a subtle background) - replacing
  the flat `TextBlock` bound to `Tutorial` in `TutorialView.axaml` and to `Explanation`/
  `FurtherExplanation` in `TutorialDetailControl.axaml`. `FurtherExplanation`'s block-level italic
  styling (previously `FontBlock.FontStyle="Italic"`) moves to a new `MarkdownTextBlock.FontStyle`
  property, since `StackPanel` (the control's base, chosen so each line gets its own wrapped
  paragraph) has no `FontStyle` of its own to inherit from in XAML. Verified by rendering the
  control headlessly (`Avalonia.Headless` + `UseSkia()`) with representative tutorial/explanation
  text covering every span kind and both list styles. Covered by new `MarkdownInlineParserTests`.
  Full solution builds with 0 warnings/errors, 490/490 tests pass (342 Core + 148 Avalonia).
- [x] T8.4.3 Step numbering, progress indicator, and prev/next step navigation. — done
  2026-10-18: `TutorialStepViewModel` now takes `stepNumber`/`totalSteps` in its constructor and
  exposes them plus `StepLabel` ("Step N:"), set by `TutorialDetailControl.RebuildSteps` from each
  hunk's index in `ProposedFileChangeViewModel.Hunks` - the same order the steps list displays them
  in. The per-step `DataTemplate` in `TutorialDetailControl.axaml` shows `StepLabel` next to the
  step title. A new `StepNavigationPanel` row above the steps `ScrollViewer` has Previous/Next
  buttons (`OnPreviousStepButtonClick`/`OnNextStepButtonClick`, disabled at the first/last step), a
  "Step X of Y" label, and a `ProgressBar`, all kept in sync by `RefreshStepNavigation()` against a
  new `_currentStepIndex` field; it's hidden entirely when a file has no resolved steps, matching
  the existing select-all row's pattern. Navigation uses `ItemsControl.ScrollIntoView(int)` (the
  plain `ItemsControl` already in use supports it directly, so no need to switch to `ListBox` and
  its unwanted selection-highlight semantics) rather than any new Core model changes - this is UI
  position tracking, not a persisted "applied" progress metric. Verified by rendering the nav
  panel/step header layout headlessly (`Avalonia.Headless` + `UseSkia()`). Full solution builds with
  0 warnings/errors, 491/491 tests pass (342 Core + 149 Avalonia).
- [x] T8.4.4 Export the tutorial to markdown/HTML. — done
  2026-10-18: new `TutorialExporter` (`Paarde.Core.Helpers`) builds a self-contained Markdown or HTML
  document from the overview plus each file's steps, numbered per file the same way
  `TutorialStepViewModel.StepLabel` (T8.4.3) numbers them in the UI - `BuildHtml` reuses
  `MarkdownInlineParser` (T8.4.2) to render explanations consistently with the in-app
  `MarkdownTextBlock` rather than a second markdown implementation, HTML-escaping everything else.
  `TutorialView` gained "Export as Markdown"/"Export as HTML" buttons bound to two new
  `TutorialViewModel` commands (disabled when there's neither an overview nor any files), which use
  a new `IFileSavePickerService` (`FileSavePickerService`, following the existing `IFolderPickerService`/
  `IFilePickerService` pattern around `IStorageProvider`) to pick a destination before writing with
  `File.WriteAllText` and reporting the result via a new `ExportStatusMessage`. Verified by running
  the exporter against representative multi-file, multi-step input and inspecting the generated
  Markdown/HTML directly. Covered by new `TutorialExporterTests` and `TutorialViewModelTests` export
  cases. Full solution builds with 0 warnings/errors, 501/501 tests pass (347 Core + 154 Avalonia).
- [x] T8.4.5 Jump from a tutorial step to the corresponding hunk in the diff viewer, and back. — done
  2026-10-18: a step's "View in Diff" button (`TutorialStepViewModel.ViewInDiffCommand`, disabled
  when `Hunk.MatchedDiffHunks` is empty - nothing resolved to jump to) navigates to the Diffs page
  with a new `DiffJumpRequest` (`Paarde.Avalonia.Models`, following the existing `PromptLaunchRequest`
  pattern) carrying the shared `ProposedFileChangeViewModel` instance, the target `DiffHunk`, and the
  originating step. `DiffsViewModel.OnNavigatedTo` selects that file and stashes the hunk as a
  "take-once" pending scroll target; `DiffsDetailControl`'s `Loaded` event (fired once this control -
  materialized per selected file via `DiffsView`'s `ContentTemplate` - is attached, by which point its
  `DataContext` is already set) reads it back via `this.FindAncestorOfType<DiffsView>()?.ViewModel` and
  scrolls the (now-named) `DiffLinesItemsControl` to `DiffHunk.SourceRangeStart` - the same
  `ItemsControl.ScrollIntoView(int)` already used for step navigation (T8.4.3). The way back is a
  "Back to Tutorial" button on the Diffs page itself (bound to a new `BackToTutorialCommand`, visible
  only when `DiffsViewModel.ReturnToStep` is set - i.e. the page was reached via a jump), which
  navigates to the Tutorial page with a new `TutorialJumpRequest` carrying the file and that step;
  `TutorialDetailControl` resolves it the same take-once-pending-target way, via its own `Loaded`
  event and `GoToStep` (T8.4.3), finding the target step by reference within the just-rebuilt `Steps`.
  Considered and rejected: restructuring `DiffsDetailControl`'s flat `DiffLines` list into
  hunk-grouped sections to add a per-hunk "jump back" affordance on every hunk - unlike the "jump
  forward, then back to where you came from" round trip this task actually describes, that would
  have changed what the Diffs page shows (unified-diff hunks omit distant unchanged context
  `DiffBuilder.GroupIntoHunks` already drops, unlike today's full-file `DiffLines` view) for no
  additional capability, so the existing flat view and a single page-level "back" action were kept.
  Likewise rejected: `CommunityToolkit.Mvvm`'s `IMessenger` (available via `ObservableRecipient` but
  unused anywhere else in the codebase) and a new Core-level navigation-state service - the existing
  `INavigationService.NavigateTo(pageKey, parameter)` plus a take-once field on each page's view
  model, already established by `HistoryViewModel.Launch()`/`PromptViewModel.OnNavigatedTo`, was
  sufficient. Covered by new `DiffsViewModelTests`, `TutorialStepViewModelTests`, and
  `TutorialViewModelTests` cases exercising both jump directions, the `CanExecute` guard, and the
  pending-target take-once behavior; the rest of the binding/navigation wiring (command bindings,
  button visibility, `ScrollIntoView` calls) is validated by Avalonia's compiled bindings at build
  time, since `AvaloniaUseCompiledBindingsByDefault` is enabled. A full interactive click-through
  was attempted via headless rendering but blocked by a pre-existing, unrelated FluentAvalonia
  `SymbolIcon` font-resolution limitation in this headless environment, not a defect in this change.
  Full solution builds with 0 warnings/errors, 508/508 tests pass (347 Core + 161 Avalonia).

---

### EPIC 9 — Quality, testing & delivery *(P1 — required by project governance)*

**F9.1 — Test foundation**
- [x] T9.1.1 Add an xUnit test project; wire coverage reporting toward the 90% floor. — done
  2026-10-02: `CodeGenToTutorial.Core.Tests` (xUnit + `coverlet.collector`), added to
  `CodeGenToTutorial.slnx`; `dotnet test --collect:"XPlat Code Coverage"` produces a Cobertura report.
- [x] T9.1.2 `DiffBuilder` tests: empty/identical/insert/delete/move, CRLF vs LF, the `MaxLcsCells`
  fallback. — done 2026-10-02, 100% line coverage on `DiffBuilder`.
- [x] T9.1.3 `ClaudeResponseParser` tests: happy path, no-files response, unstructured response,
  language tags on fences, nested fences, multiple files, multiple steps, truncated input. — done
  2026-10-02, 100% line coverage; the nested-fence and truncated-fence tests pin today's known-fragile
  behavior (see "Protocol fragility" above) as a baseline for the Epic 5 rework.
- [x] T9.1.4 `FileChangeApplyService` tests: new file, nested directory creation, **path-traversal
  rejection**, permission failure. — done 2026-10-02, 100% line coverage, covers the T4.1.1 fix.
- [x] T9.1.5 ViewModel tests with a faked `ILlmRunner`. — done 2026-10-06: new
  `CodeGenToTutorial.Avalonia.Tests` project (hand-written fakes for `IClaudeCliService`,
  `ILocalSettingsService`, `IFolderPickerService`, `IPromptResultStore`, matching the existing
  no-mocking-library convention) covers `PromptViewModel` — success with/without files, non-zero
  exit code, CLI exceptions, `CanRunPrompt` gating, save/browse commands, and settings restore on
  navigation. There's no `ILlmRunner` abstraction yet (that's T6.3.1); faked `IClaudeCliService`
  directly, which is the only LLM-runner dependency that exists today.

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
