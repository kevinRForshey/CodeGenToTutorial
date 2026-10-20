using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using Paarde.Core.Models;

namespace Paarde.Avalonia.Controls;

// T8.3.3: renders one diff line's text as plain runs, with the tokens DiffBuilder.BuildWordDiff
// found to have actually changed given a stronger highlight than the row's own
// DiffLineKindToBackgroundConverter background. WordSpans is null for an Unchanged line, a
// side-by-side padding cell, or a change that never paired with a partner to diff against - those
// just render LineText as one plain run, the same as a normal TextBlock would.
public class DiffWordSpanTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> LineTextProperty =
        AvaloniaProperty.Register<DiffWordSpanTextBlock, string?>(nameof(LineText));

    public static readonly StyledProperty<IReadOnlyList<DiffLine>?> WordSpansProperty =
        AvaloniaProperty.Register<DiffWordSpanTextBlock, IReadOnlyList<DiffLine>?>(nameof(WordSpans));

    public string? LineText
    {
        get => GetValue(LineTextProperty);
        set => SetValue(LineTextProperty, value);
    }

    public IReadOnlyList<DiffLine>? WordSpans
    {
        get => GetValue(WordSpansProperty);
        set => SetValue(WordSpansProperty, value);
    }

    // Same hue as DiffLineKindToBackgroundConverter's Added/Removed row backgrounds, but a much
    // higher alpha - a changed word needs to stand out against a row that's already tinted.
    private static readonly SolidColorBrush AddedWordBrush = new(Color.FromArgb(110, 46, 160, 67));
    private static readonly SolidColorBrush RemovedWordBrush = new(Color.FromArgb(110, 218, 54, 51));

    static DiffWordSpanTextBlock()
    {
        LineTextProperty.Changed.AddClassHandler<DiffWordSpanTextBlock>((control, _) => control.Rebuild());
        WordSpansProperty.Changed.AddClassHandler<DiffWordSpanTextBlock>((control, _) => control.Rebuild());
    }

    private void Rebuild()
    {
        Inlines!.Clear();

        if (WordSpans is null)
        {
            if (!string.IsNullOrEmpty(LineText))
            {
                Inlines.Add(new Run(LineText));
            }

            return;
        }

        foreach (var span in WordSpans)
        {
            var run = new Run(span.Text);

            run.Background = span.Kind switch
            {
                DiffLineKind.Added => AddedWordBrush,
                DiffLineKind.Removed => RemovedWordBrush,
                _ => null,
            };

            Inlines.Add(run);
        }
    }
}
