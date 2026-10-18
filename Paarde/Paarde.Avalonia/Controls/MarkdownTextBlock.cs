using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using Paarde.Core.Helpers;
using Paarde.Core.Models;

namespace Paarde.Avalonia.Controls;

// Renders MarkdownInlineParser's output as one wrapped TextBlock per source line - indented and
// bulleted for list items - instead of the single flat TextBlock the tutorial/explanation panes
// used before T8.4.2.
public class MarkdownTextBlock : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, string?>(nameof(Text));

    // StackPanel (unlike TextBlock) has no FontStyle of its own, so callers that want the whole
    // block italicized (e.g. the "further explanation" pane) need this instead of the usual
    // inherited TextElement.FontStyle.
    public static readonly StyledProperty<FontStyle> FontStyleProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, FontStyle>(nameof(FontStyle));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FontStyle FontStyle
    {
        get => GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    static MarkdownTextBlock()
    {
        TextProperty.Changed.AddClassHandler<MarkdownTextBlock>((control, _) => control.Rebuild());
        FontStyleProperty.Changed.AddClassHandler<MarkdownTextBlock>((control, _) => control.Rebuild());
    }

    public MarkdownTextBlock()
    {
        Spacing = 4;
    }

    private void Rebuild()
    {
        Children.Clear();

        foreach (var line in MarkdownInlineParser.Parse(Text))
        {
            Children.Add(BuildLine(line, FontStyle));
        }
    }

    private static TextBlock BuildLine(MarkdownLine line, FontStyle baseFontStyle)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontStyle = baseFontStyle,
            Margin = line.IsListItem ? new Thickness(16, 0, 0, 0) : default,
        };

        if (line.IsListItem)
        {
            textBlock.Inlines!.Add(new Run("•  ") { FontStyle = baseFontStyle });
        }

        foreach (var span in line.Spans)
        {
            textBlock.Inlines!.Add(BuildRun(span, baseFontStyle));
        }

        return textBlock;
    }

    private static Run BuildRun(MarkdownSpan span, FontStyle baseFontStyle)
    {
        var run = new Run(span.Text) { FontStyle = baseFontStyle };

        switch (span.Kind)
        {
            case MarkdownSpanKind.Bold:
                run.FontWeight = FontWeight.Bold;
                break;
            case MarkdownSpanKind.Italic:
                run.FontStyle = FontStyle.Italic;
                break;
            case MarkdownSpanKind.Code:
                run.FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace");
                run.Background = new SolidColorBrush(Colors.Gray, 0.3);
                break;
        }

        return run;
    }
}
