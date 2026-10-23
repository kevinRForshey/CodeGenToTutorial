using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

using AvaloniaEdit;
using AvaloniaEdit.TextMate;

using Paarde.Avalonia.Helpers;
using Paarde.Core.Models;

using TextMateSharp.Grammars;

namespace Paarde.Avalonia.Controls;

// Epic 15, T15.1.1: raised by CodeEditor after its debounce window elapses while the caret sits in
// typed text, carrying just enough context (text immediately before/after the caret) for a host
// ViewModel to request a short completion - same shape as DiffHunkAcceptRejectEventArgs, a plain
// data-carrying EventArgs rather than a callback, so CodeEditor stays ignorant of ILlmRunner.
public sealed class GhostTextRequestedEventArgs(string textBeforeCaret, string textAfterCaret) : EventArgs
{
    public string TextBeforeCaret { get; } = textBeforeCaret;

    public string TextAfterCaret { get; } = textAfterCaret;
}

// Epic 17, T17.1.3: raised by CodeEditor on F12/Shift+F12, carrying just the caret's LSP position -
// shared by both GoToDefinitionRequested and FindReferencesRequested since neither needs anything
// beyond where the caret was when the key was pressed.
public sealed class CodeNavigationRequestedEventArgs(LspPosition position) : EventArgs
{
    public LspPosition Position { get; } = position;
}

// Thin wrapper around AvaloniaEdit's TextEditor that wires up the three things a code viewer
// needs out of the box: line numbers, TextMate syntax highlighting picked from the bound file
// path's extension, and bracket matching (which AvaloniaEdit doesn't provide itself). Two-way
// editing (T2.2.2+) is intentionally not addressed here.
public class CodeEditor : TextEditor
{
    private static readonly RegistryOptions RegistryOptions = new(ThemeName.DarkPlus);

    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<CodeEditor, string?>(nameof(FilePath));

    // T13.1.2: null (not set) for every CodeEditor host except TutorialDetailControl's editable
    // proposal view - Explorer's read-only preview never binds these, so the renderer/margin stay
    // empty and invisible there.
    public static readonly StyledProperty<IReadOnlyList<DiffLine>?> DiffLinesProperty =
        AvaloniaProperty.Register<CodeEditor, IReadOnlyList<DiffLine>?>(nameof(DiffLines));

    public static readonly StyledProperty<IReadOnlyList<DiffHunk>?> DiffHunksProperty =
        AvaloniaProperty.Register<CodeEditor, IReadOnlyList<DiffHunk>?>(nameof(DiffHunks));

    // Epic 15, T15.1.1: set by the host ViewModel once a completion for the most recent
    // GhostTextRequested arrives (or left alone - CodeEditor ignores it as stale - if the caret has
    // since moved away from the offset the request was made at).
    public static readonly StyledProperty<string?> GhostTextSuggestionProperty =
        AvaloniaProperty.Register<CodeEditor, string?>(nameof(GhostTextSuggestion));

    // Epic 17, T17.1.2: null (not set) for every CodeEditor host except Explorer's - same
    // opt-in-by-binding convention DiffLines/DiffHunks above already use for their own
    // Explorer/Tutorial split.
    public static readonly StyledProperty<IReadOnlyList<LspDiagnostic>?> DiagnosticsProperty =
        AvaloniaProperty.Register<CodeEditor, IReadOnlyList<LspDiagnostic>?>(nameof(Diagnostics));

    private readonly TextMate.Installation _textMateInstallation;
    private readonly BracketHighlightRenderer _bracketHighlightRenderer;
    private readonly DiffBackgroundRenderer _diffBackgroundRenderer;
    private readonly DiffHunkGutterMargin _diffHunkGutterMargin;
    private readonly DiagnosticsRenderer _diagnosticsRenderer;
    private LspDiagnostic? _shownDiagnosticTooltip;

    private readonly GhostTextRenderer _ghostTextRenderer;
    private readonly GhostTextSuggestionState _ghostTextSuggestionState = new();
    private readonly GhostTextDebouncer _ghostTextDebouncer = new(TimeSpan.FromMilliseconds(400));
    private readonly DispatcherTimer _ghostTextTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private bool _isApplyingGhostTextSuggestion;
    private int _pendingGhostTextRequestOffset = -1;

    // TemplatedControl resolves its default ControlTheme via StyleKeyOverride, which defaults to
    // GetType(). Without this override Avalonia looks for a theme keyed {x:Type CodeEditor} - which
    // doesn't exist - instead of reusing TextEditor's theme, so no template (and therefore nothing:
    // no background, no text view) ever gets applied to this subclass.
    protected override Type StyleKeyOverride => typeof(TextEditor);

    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public IReadOnlyList<DiffLine>? DiffLines
    {
        get => GetValue(DiffLinesProperty);
        set => SetValue(DiffLinesProperty, value);
    }

    public IReadOnlyList<DiffHunk>? DiffHunks
    {
        get => GetValue(DiffHunksProperty);
        set => SetValue(DiffHunksProperty, value);
    }

    public string? GhostTextSuggestion
    {
        get => GetValue(GhostTextSuggestionProperty);
        set => SetValue(GhostTextSuggestionProperty, value);
    }

    public IReadOnlyList<LspDiagnostic>? Diagnostics
    {
        get => GetValue(DiagnosticsProperty);
        set => SetValue(DiagnosticsProperty, value);
    }

    // T13.1.3: raised when the gutter's Accept/Reject button for a hunk is clicked; the host
    // (TutorialDetailControl) is the one that knows which ProposedFileChangeViewModel owns that
    // hunk's selection, so this control just forwards which DiffHunk and which action.
    public event EventHandler<DiffHunkAcceptRejectEventArgs>? DiffHunkActionRequested;

    // Epic 15, T15.1.2/T15.2: raised after the debounce window elapses while the caret sits in
    // typed text - the host ViewModel is the one that knows which ILlmRunner to ask and how, so
    // this control just forwards the surrounding text and (via GhostTextSuggestion) receives the
    // answer back.
    public event EventHandler<GhostTextRequestedEventArgs>? GhostTextRequested;

    // Epic 17, T17.1.3: the host ViewModel is the one that knows how to ask ILanguageServerClient
    // and what to do with the result (jump the tree selection, show a references flyout), so this
    // control just forwards where the caret was.
    public event EventHandler<CodeNavigationRequestedEventArgs>? GoToDefinitionRequested;

    public event EventHandler<CodeNavigationRequestedEventArgs>? FindReferencesRequested;

    static CodeEditor()
    {
        FilePathProperty.Changed.AddClassHandler<CodeEditor>((editor, _) => editor.ApplyGrammarForFilePath());
        DiffLinesProperty.Changed.AddClassHandler<CodeEditor>((editor, _) => editor._diffBackgroundRenderer.UpdateDiffLines(editor.DiffLines));
        DiffHunksProperty.Changed.AddClassHandler<CodeEditor>((editor, _) => editor._diffHunkGutterMargin.UpdateHunks(editor.DiffHunks));
        GhostTextSuggestionProperty.Changed.AddClassHandler<CodeEditor>((editor, _) => editor.OnGhostTextSuggestionChanged());
        DiagnosticsProperty.Changed.AddClassHandler<CodeEditor>((editor, _) => editor._diagnosticsRenderer.UpdateDiagnostics(editor.Diagnostics));
    }

    public CodeEditor()
    {
        ShowLineNumbers = true;
        FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace");

        _textMateInstallation = this.InstallTextMate(RegistryOptions);
        _bracketHighlightRenderer = new BracketHighlightRenderer(TextArea.TextView);
        TextArea.Caret.PositionChanged += (_, _) => _bracketHighlightRenderer.UpdateHighlight(Document, CaretOffset);

        _diffBackgroundRenderer = new DiffBackgroundRenderer(TextArea.TextView);

        _diffHunkGutterMargin = new DiffHunkGutterMargin();
        _diffHunkGutterMargin.HunkActionRequested += (_, e) => DiffHunkActionRequested?.Invoke(this, e);
        TextArea.LeftMargins.Add(_diffHunkGutterMargin);

        _ghostTextRenderer = new GhostTextRenderer(TextArea.TextView);
        _ghostTextTimer.Tick += (_, _) => OnGhostTextTimerTick();
        _ghostTextTimer.Start();
        TextChanged += (_, _) => OnCodeEditorTextChanged();

        _diagnosticsRenderer = new DiagnosticsRenderer(TextArea.TextView);
        PointerMoved += OnPointerMovedForDiagnostics;
        PointerExited += (_, _) => HideDiagnosticTooltip();
        KeyDown += OnCodeEditorKeyDownForNavigation;

        // Tunnel runs top-down before TextArea's own OnKeyDown override (which unconditionally
        // handles Tab when Options.AcceptsTab is set) reaches it, so this gets first look at Tab
        // while a suggestion is showing; handledEventsToo isn't needed here since we're the ones
        // marking it handled, not reacting to someone else having done so.
        TextArea.AddHandler(KeyDownEvent, OnTextAreaKeyDown, RoutingStrategies.Tunnel);

        // AvaloniaEdit.TextMate only recolors syntax - it never touches the editor's own
        // Background/Foreground, so without this the DarkPlus theme's light foreground text is drawn
        // on whatever background the app's current Fluent theme happens to default to (invisible
        // against a light background). InstallTextMate already applied the theme once by the time it
        // returns (its constructor calls SetTheme before we could ever subscribe to AppliedTheme), so
        // apply the colors directly here instead of relying on that event.
        if (_textMateInstallation.TryGetThemeColor("editor.background", out var background) && Color.TryParse(background, out var backgroundColor))
        {
            Background = new SolidColorBrush(backgroundColor);
        }

        if (_textMateInstallation.TryGetThemeColor("editor.foreground", out var foreground) && Color.TryParse(foreground, out var foregroundColor))
        {
            Foreground = new SolidColorBrush(foregroundColor);
        }
    }

    private void ApplyGrammarForFilePath()
    {
        var extension = string.IsNullOrEmpty(FilePath) ? null : Path.GetExtension(FilePath);
        var language = string.IsNullOrEmpty(extension) ? null : RegistryOptions.GetLanguageByExtension(extension);

        _textMateInstallation.SetGrammar(language is null ? null : RegistryOptions.GetScopeByLanguageId(language.Id));
    }

    // Any edit (including the one GhostTextRequested's own debounce window is about to react to)
    // invalidates whatever's currently showing - a suggestion only ever applies to the exact text
    // it was computed against - and restarts the debounce window. The suggestion just accepted via
    // Tab (below) also raises this event, which would otherwise immediately dismiss what was just
    // inserted; _isApplyingGhostTextSuggestion skips that one case.
    private void OnCodeEditorTextChanged()
    {
        if (_isApplyingGhostTextSuggestion)
        {
            return;
        }

        DismissGhostTextSuggestion();
        _ghostTextDebouncer.NotifyInput(DateTimeOffset.UtcNow);
    }

    private void OnGhostTextTimerTick()
    {
        if (!_ghostTextDebouncer.ShouldFire(DateTimeOffset.UtcNow))
        {
            return;
        }

        if (IsReadOnly || Document is null || GhostTextRequested is null)
        {
            return;
        }

        var caretOffset = CaretOffset;
        var text = Document.Text;

        // T15.2.1: a short window around the caret, not the whole file - this runs on essentially
        // every keystroke-adjacent pause, so the prompt needs to stay cheap regardless of file size.
        const int maxBeforeLength = 4000;
        const int maxAfterLength = 1000;

        var textBeforeCaret = text[..caretOffset];
        if (textBeforeCaret.Length > maxBeforeLength)
        {
            textBeforeCaret = textBeforeCaret[^maxBeforeLength..];
        }

        var textAfterCaret = text[caretOffset..];
        if (textAfterCaret.Length > maxAfterLength)
        {
            textAfterCaret = textAfterCaret[..maxAfterLength];
        }

        _pendingGhostTextRequestOffset = caretOffset;
        GhostTextRequested.Invoke(this, new GhostTextRequestedEventArgs(textBeforeCaret, textAfterCaret));
    }

    // The host ViewModel set GhostTextSuggestion in response to the GhostTextRequested this control
    // raised earlier - but that was an async round trip, so the caret may have moved on by now. If
    // it has, this is stale and gets dropped rather than popping up a suggestion for text the user
    // isn't at anymore.
    private void OnGhostTextSuggestionChanged()
    {
        var suggestion = GhostTextSuggestion;
        if (string.IsNullOrEmpty(suggestion) || _pendingGhostTextRequestOffset != CaretOffset)
        {
            return;
        }

        _ghostTextSuggestionState.Show(_pendingGhostTextRequestOffset, suggestion);
        _ghostTextRenderer.UpdateSuggestion(_pendingGhostTextRequestOffset, suggestion);
    }

    private void OnTextAreaKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_ghostTextSuggestionState.HasSuggestion)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            DismissGhostTextSuggestion();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Tab)
        {
            return;
        }

        var insertText = _ghostTextSuggestionState.Accept(CaretOffset);
        if (insertText is null)
        {
            return;
        }

        _isApplyingGhostTextSuggestion = true;
        try
        {
            var insertOffset = CaretOffset;
            Document!.Insert(insertOffset, insertText);
            CaretOffset = insertOffset + insertText.Length;
        }
        finally
        {
            _isApplyingGhostTextSuggestion = false;
        }

        _ghostTextRenderer.Clear();
        e.Handled = true;
    }

    private void DismissGhostTextSuggestion()
    {
        _ghostTextSuggestionState.Dismiss();
        _ghostTextRenderer.Clear();
        _ghostTextDebouncer.Cancel();
    }

    // Epic 17, T17.1.2: GetPositionFromPoint (inherited from TextEditor) already translates a
    // point relative to this control into document coordinates, scroll offset included - no manual
    // ScrollOffset adjustment needed here.
    private void OnPointerMovedForDiagnostics(object? sender, PointerEventArgs e)
    {
        if (Document is null || GetPositionFromPoint(e.GetPosition(this)) is not { } position)
        {
            HideDiagnosticTooltip();
            return;
        }

        var offset = Document.GetOffset(position.Location);
        var diagnostic = _diagnosticsRenderer.FindAt(Document, offset);
        if (diagnostic is null)
        {
            HideDiagnosticTooltip();
            return;
        }

        if (diagnostic.Equals(_shownDiagnosticTooltip))
        {
            return;
        }

        _shownDiagnosticTooltip = diagnostic;

        // Forcing a close/reopen (rather than just updating Tip while a tooltip is already shown)
        // guarantees the new message actually (re)displays - Avalonia's own hover-delay behavior
        // doesn't reliably refresh on content changes alone.
        ToolTip.SetIsOpen(this, false);
        ToolTip.SetTip(this, diagnostic.Message);
        ToolTip.SetIsOpen(this, true);
    }

    private void HideDiagnosticTooltip()
    {
        if (_shownDiagnosticTooltip is null)
        {
            return;
        }

        _shownDiagnosticTooltip = null;
        ToolTip.SetIsOpen(this, false);
    }

    // Epic 17, T17.1.3: F12 (go to definition) / Shift+F12 (find references) - the same keybinds
    // most LSP-backed editors (VS Code, Visual Studio) already use for these, so no settings/
    // customization is needed for day one.
    private void OnCodeEditorKeyDownForNavigation(object? sender, KeyEventArgs e)
    {
        if (Document is null || e.Key != Key.F12)
        {
            return;
        }

        var position = ToLspPosition(CaretOffset);

        if (e.KeyModifiers == KeyModifiers.Shift)
        {
            FindReferencesRequested?.Invoke(this, new CodeNavigationRequestedEventArgs(position));
            e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            GoToDefinitionRequested?.Invoke(this, new CodeNavigationRequestedEventArgs(position));
            e.Handled = true;
        }
    }

    private LspPosition ToLspPosition(int offset)
    {
        var location = Document!.GetLocation(offset);
        return new LspPosition(location.Line - 1, location.Column - 1);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _ghostTextTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}
