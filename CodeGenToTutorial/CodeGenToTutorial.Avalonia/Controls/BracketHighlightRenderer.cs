using System.Collections.Generic;

using Avalonia.Media;

using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace CodeGenToTutorial.Avalonia.Controls;

// Minimal, language-agnostic bracket matcher: scans the raw document text (not grammar-aware, so
// it doesn't skip brackets inside strings/comments) for a (), [], or {} pair touching the caret
// and highlights both sides. AvaloniaEdit has no built-in bracket matcher, unlike its line
// numbers/syntax highlighting which TextMate provides.
internal sealed class BracketHighlightRenderer : IBackgroundRenderer
{
    private static readonly Dictionary<char, char> OpenToClose = new() { ['('] = ')', ['['] = ']', ['{'] = '}' };
    private static readonly Dictionary<char, char> CloseToOpen = new() { [')'] = '(', [']'] = '[', ['}'] = '{' };

    private readonly TextView _textView;
    private int _openOffset = -1;
    private int _closeOffset = -1;

    public KnownLayer Layer => KnownLayer.Selection;

    public IBrush BackgroundBrush { get; set; } = new SolidColorBrush(Color.FromArgb(90, 128, 128, 128));

    public BracketHighlightRenderer(TextView textView)
    {
        _textView = textView;
        _textView.BackgroundRenderers.Add(this);
    }

    public void UpdateHighlight(TextDocument? document, int caretOffset)
    {
        var (openOffset, closeOffset) = document is null ? (-1, -1) : FindMatchingPair(document.Text, caretOffset);
        if (openOffset == _openOffset && closeOffset == _closeOffset)
        {
            return;
        }

        _openOffset = openOffset;
        _closeOffset = closeOffset;
        _textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_openOffset < 0)
        {
            return;
        }

        var builder = new BackgroundGeometryBuilder { CornerRadius = 1 };
        builder.AddSegment(textView, new TextSegment { StartOffset = _openOffset, Length = 1 });
        builder.AddSegment(textView, new TextSegment { StartOffset = _closeOffset, Length = 1 });

        var geometry = builder.CreateGeometry();
        if (geometry != null)
        {
            drawingContext.DrawGeometry(BackgroundBrush, null, geometry);
        }
    }

    internal static (int Open, int Close) FindMatchingPair(string text, int caretOffset)
    {
        foreach (var candidate in new[] { caretOffset - 1, caretOffset })
        {
            if (candidate < 0 || candidate >= text.Length)
            {
                continue;
            }

            var c = text[candidate];
            if (OpenToClose.TryGetValue(c, out var closeChar))
            {
                var close = FindForward(text, candidate, c, closeChar);
                if (close >= 0)
                {
                    return (candidate, close);
                }
            }
            else if (CloseToOpen.TryGetValue(c, out var openChar))
            {
                var open = FindBackward(text, candidate, openChar, c);
                if (open >= 0)
                {
                    return (open, candidate);
                }
            }
        }

        return (-1, -1);
    }

    private static int FindForward(string text, int startOffset, char open, char close)
    {
        var depth = 0;
        for (var i = startOffset; i < text.Length; i++)
        {
            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static int FindBackward(string text, int startOffset, char open, char close)
    {
        var depth = 0;
        for (var i = startOffset; i >= 0; i--)
        {
            if (text[i] == close)
            {
                depth++;
            }
            else if (text[i] == open)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }
}
