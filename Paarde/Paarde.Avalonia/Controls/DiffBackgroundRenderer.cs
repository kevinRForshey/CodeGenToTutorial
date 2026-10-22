using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Media;

using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

using Paarde.Core.Models;

namespace Paarde.Avalonia.Controls;

// A proposed-side line range to paint: an added run (LineCount > 0, 1-based inclusive start) or a
// pure-deletion marker (LineCount == 0 - no proposed-side lines to anchor a band to, so StartLine is
// the insertion point the deleted lines used to sit before).
internal readonly record struct DiffBand(int StartLine, int LineCount, bool IsAddition);

// Paints translucent green bands behind added lines and a thin red marker at each pure deletion's
// insertion point, directly over the editable proposal view (TutorialDetailControl's editor) the
// same way BracketHighlightRenderer paints bracket matches - same IBackgroundRenderer extension
// point, no new one introduced. Reuses DiffBuilder's existing line-level diff
// (ProposedFileChangeViewModel.DiffLines) rather than a second diff algorithm. ComputeBands is the
// pure line-range mapping, kept separate from Draw so it's unit-testable without a running Avalonia
// TextView (T13.3.1) - AvaloniaEdit's actual paint call isn't unit-testable the same way
// BracketHighlightRenderer's Draw isn't.
internal sealed class DiffBackgroundRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private IReadOnlyList<DiffLine> _diffLines = Array.Empty<DiffLine>();

    public KnownLayer Layer => KnownLayer.Background;

    public IBrush AddedBrush { get; set; } = new SolidColorBrush(Color.FromArgb(60, 46, 160, 67));

    public IBrush RemovedMarkerBrush { get; set; } = new SolidColorBrush(Color.FromArgb(200, 203, 36, 49));

    public DiffBackgroundRenderer(TextView textView)
    {
        _textView = textView;
        _textView.BackgroundRenderers.Add(this);
    }

    // T13.2.1: replaces _diffLines wholesale every time the bound DiffLines change (an edit, or
    // OriginalContent being refreshed from disk) rather than caching offsets once at load, so the
    // bands painted next never describe a stale diff.
    public void UpdateDiffLines(IReadOnlyList<DiffLine>? diffLines)
    {
        _diffLines = diffLines ?? Array.Empty<DiffLine>();
        _textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_diffLines.Count == 0 || textView.Document is null || !textView.VisualLinesValid)
        {
            return;
        }

        foreach (var band in ComputeBands(_diffLines))
        {
            if (band.IsAddition)
            {
                DrawAddedBand(textView, drawingContext, band);
            }
            else
            {
                DrawRemovedMarker(textView, drawingContext, band.StartLine);
            }
        }
    }

    private void DrawAddedBand(TextView textView, DrawingContext drawingContext, DiffBand band)
    {
        var document = textView.Document;
        if (band.StartLine < 1 || band.StartLine > document.LineCount)
        {
            return;
        }

        var endLine = Math.Min(band.StartLine + band.LineCount - 1, document.LineCount);
        var startOffset = document.GetLineByNumber(band.StartLine).Offset;
        var endLineInfo = document.GetLineByNumber(endLine);

        var builder = new BackgroundGeometryBuilder
        {
            AlignToWholePixels = true,
            CornerRadius = 0,
            ExtendToFullWidthAtLineEnd = true,
        };
        builder.AddSegment(textView, new TextSegment { StartOffset = startOffset, Length = endLineInfo.EndOffset - startOffset });

        var geometry = builder.CreateGeometry();
        if (geometry != null)
        {
            drawingContext.DrawGeometry(AddedBrush, null, geometry);
        }
    }

    private void DrawRemovedMarker(TextView textView, DrawingContext drawingContext, int anchorLine)
    {
        var document = textView.Document;
        var lineCount = document.LineCount;
        if (lineCount == 0)
        {
            return;
        }

        var targetLine = Math.Clamp(anchorLine, 1, lineCount);
        var visualLine = textView.GetVisualLine(targetLine);
        if (visualLine is null)
        {
            // Not currently scrolled into view - nothing to draw this frame, same as how AddSegment
            // above silently produces no geometry for offscreen offsets.
            return;
        }

        // A deletion past the last line (content removed from the end of the file) renders below
        // that line instead of above it; every other case renders above the anchor line, matching
        // where the removed content used to sit relative to what's left.
        var y = anchorLine > lineCount ? visualLine.VisualTop + visualLine.Height : visualLine.VisualTop;
        y -= textView.ScrollOffset.Y;

        var builder = new BackgroundGeometryBuilder();
        builder.AddRectangle(textView, new Rect(0, y - 1, textView.Bounds.Width, 2));

        var geometry = builder.CreateGeometry();
        if (geometry != null)
        {
            drawingContext.DrawGeometry(RemovedMarkerBrush, null, geometry);
        }
    }

    // Walks the flat line diff once, tracking the running 1-based line number on the proposed side
    // (the only side the editor actually shows - EditedContent). Consecutive Added lines coalesce
    // into one band; a run of one or more consecutive Removed lines (which consume no proposed-side
    // line number) collapses to a single zero-length marker at the boundary they'd have sat at,
    // rather than one marker per removed line.
    internal static IReadOnlyList<DiffBand> ComputeBands(IReadOnlyList<DiffLine> diffLines)
    {
        var bands = new List<DiffBand>();
        var proposedLine = 1;
        var addedRunStart = -1;
        var pendingRemovedMarker = false;

        foreach (var line in diffLines)
        {
            if (line.Kind == DiffLineKind.Added)
            {
                if (addedRunStart < 0)
                {
                    addedRunStart = proposedLine;
                }

                proposedLine++;
                pendingRemovedMarker = false;
                continue;
            }

            if (addedRunStart >= 0)
            {
                bands.Add(new DiffBand(addedRunStart, proposedLine - addedRunStart, IsAddition: true));
                addedRunStart = -1;
            }

            if (line.Kind == DiffLineKind.Removed)
            {
                if (!pendingRemovedMarker)
                {
                    bands.Add(new DiffBand(proposedLine, 0, IsAddition: false));
                    pendingRemovedMarker = true;
                }

                continue;
            }

            // Unchanged
            proposedLine++;
            pendingRemovedMarker = false;
        }

        if (addedRunStart >= 0)
        {
            bands.Add(new DiffBand(addedRunStart, proposedLine - addedRunStart, IsAddition: true));
        }

        return bands;
    }
}
