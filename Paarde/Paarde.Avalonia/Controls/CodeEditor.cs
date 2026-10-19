using System;
using System.IO;

using Avalonia;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.TextMate;

using TextMateSharp.Grammars;

namespace Paarde.Avalonia.Controls;

// Thin wrapper around AvaloniaEdit's TextEditor that wires up the three things a code viewer
// needs out of the box: line numbers, TextMate syntax highlighting picked from the bound file
// path's extension, and bracket matching (which AvaloniaEdit doesn't provide itself). Two-way
// editing (T2.2.2+) is intentionally not addressed here.
public class CodeEditor : TextEditor
{
    private static readonly RegistryOptions RegistryOptions = new(ThemeName.DarkPlus);

    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<CodeEditor, string?>(nameof(FilePath));

    private readonly TextMate.Installation _textMateInstallation;
    private readonly BracketHighlightRenderer _bracketHighlightRenderer;

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

    static CodeEditor()
    {
        FilePathProperty.Changed.AddClassHandler<CodeEditor>((editor, _) => editor.ApplyGrammarForFilePath());
    }

    public CodeEditor()
    {
        ShowLineNumbers = true;
        FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace");

        _textMateInstallation = this.InstallTextMate(RegistryOptions);
        _bracketHighlightRenderer = new BracketHighlightRenderer(TextArea.TextView);
        TextArea.Caret.PositionChanged += (_, _) => _bracketHighlightRenderer.UpdateHighlight(Document, CaretOffset);

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
}
