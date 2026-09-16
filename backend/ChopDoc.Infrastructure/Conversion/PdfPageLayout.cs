using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace ChopDoc.Infrastructure.Conversion;

/// <summary>
/// Rebuilds reading order from word boxes so columns and wrapped lines are not concatenated.
/// </summary>
internal static class PdfPageLayout
{
    private const double SameLineTolerance = 3.0;
    private const double SameLineMaxGap = 28.0;
    private const double MinColumnGap = 36.0;
    private const double HeadingMinHeight = 11.0;
    private const double FooterMaxBaseline = 28.0;
    private const double BulletContinuationIndent = 8.0;

    public static IReadOnlyList<LayoutBlock> ExtractBlocks(Page page)
    {
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .Select(w => new GlyphWord(w.Text.Trim(), w.BoundingBox))
            .Where(w => w.Text.Length > 0)
            .ToList();

        if (words.Count == 0)
            return Array.Empty<LayoutBlock>();

        var lines = GroupLines(words);
        var ordered = OrderLines(lines, page.Width);
        return ToBlocks(ordered);
    }

    private static List<TextLine> GroupLines(List<GlyphWord> words)
    {
        var sorted = words
            .OrderByDescending(w => w.Bottom)
            .ThenBy(w => w.Left)
            .ToList();

        var lines = new List<TextLine>();
        foreach (var word in sorted)
        {
            var match = lines
                .Where(line =>
                    Math.Abs(line.Bottom - word.Bottom) <= SameLineTolerance &&
                    word.Left >= line.Right - 1.5 &&
                    word.Left - line.Right <= SameLineMaxGap)
                .OrderBy(line => word.Left - line.Right)
                .FirstOrDefault();

            if (match is null)
                lines.Add(new TextLine(word));
            else
                match.Add(word);
        }

        return lines;
    }

    private static List<TextLine> OrderLines(List<TextLine> lines, double pageWidth)
    {
        var footer = lines.Where(l => l.Bottom < FooterMaxBaseline && l.Height < HeadingMinHeight).ToList();
        var body = lines.Except(footer).ToList();
        var split = FindColumnSplit(body, pageWidth);

        if (split is null)
        {
            return body.OrderByDescending(l => l.Bottom)
                .Concat(footer.OrderByDescending(l => l.Bottom))
                .ToList();
        }

        var left = body.Where(l => l.Left < split.Value).OrderByDescending(l => l.Bottom).ToList();
        var right = body.Where(l => l.Left >= split.Value).OrderByDescending(l => l.Bottom).ToList();
        var merged = SpliceRightClusters(left, ClusterByGap(right, 30));
        return merged.Concat(footer.OrderByDescending(l => l.Bottom)).ToList();
    }

    private static List<List<TextLine>> ClusterByGap(IReadOnlyList<TextLine> lines, double gap)
    {
        var clusters = new List<List<TextLine>>();
        List<TextLine>? current = null;
        double? previousBottom = null;

        foreach (var line in lines)
        {
            if (current is null || previousBottom is null || previousBottom.Value - line.Bottom > gap)
            {
                current = new List<TextLine>();
                clusters.Add(current);
            }

            current.Add(line);
            previousBottom = line.Bottom;
        }

        return clusters;
    }

    /// <summary>
    /// Insert each right-hand callout after the left lines that sit beside it, instead of dumping the column at the end.
    /// </summary>
    private static List<TextLine> SpliceRightClusters(List<TextLine> left, IReadOnlyList<List<TextLine>> clusters)
    {
        var flow = new List<TextLine>(left);
        foreach (var cluster in clusters)
        {
            var floor = cluster.Min(l => l.Bottom);
            var insertAt = 0;
            for (var i = 0; i < flow.Count; i++)
            {
                if (flow[i].Bottom >= floor)
                    insertAt = i + 1;
            }

            flow.InsertRange(insertAt, cluster);
        }

        return flow;
    }

    private static double? FindColumnSplit(IReadOnlyList<TextLine> lines, double pageWidth)
    {
        if (lines.Count < 6)
            return null;

        var lefts = lines.Select(l => l.Left).OrderBy(x => x).ToList();
        double bestGap = 0;
        double split = 0;
        for (var i = 1; i < lefts.Count; i++)
        {
            var gap = lefts[i] - lefts[i - 1];
            if (gap <= bestGap)
                continue;

            bestGap = gap;
            split = (lefts[i] + lefts[i - 1]) / 2.0;
        }

        if (bestGap < MinColumnGap || split < pageWidth * 0.28 || split > pageWidth * 0.72)
            return null;

        var left = lines.Where(l => l.Left < split).ToList();
        var right = lines.Where(l => l.Left >= split).ToList();
        if (left.Count < 3 || right.Count < 2)
            return null;

        var overlaps = left.Any(l => right.Any(r =>
            Math.Min(l.Top, r.Top) - Math.Max(l.Bottom, r.Bottom) > 1));

        return overlaps ? split : null;
    }

    private static IReadOnlyList<LayoutBlock> ToBlocks(IReadOnlyList<TextLine> lines)
    {
        const double paragraphGap = 16.5;

        var blocks = new List<LayoutBlock>();
        var paragraph = new List<string>();
        string? bullet = null;
        double bulletIndent = 0;
        double? previousBottom = null;
        double? previousLeft = null;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
                return;

            blocks.Add(new LayoutBlock("p", string.Join(" ", paragraph)));
            paragraph.Clear();
        }

        void FlushBullet()
        {
            if (bullet is null)
                return;

            blocks.Add(new LayoutBlock("li", bullet));
            bullet = null;
        }

        foreach (var line in lines)
        {
            var text = CollapseSpaces(line.Text);
            if (text.Length == 0)
                continue;

            var isHeading = line.Height >= HeadingMinHeight && text.Length <= 90 && !StartsWithBullet(text);
            if (isHeading)
            {
                FlushBullet();
                FlushParagraph();
                blocks.Add(new LayoutBlock("h3", text));
                previousBottom = line.Bottom;
                previousLeft = line.Left;
                continue;
            }

            if (StartsWithBullet(text))
            {
                FlushParagraph();
                FlushBullet();
                bullet = StripBullet(text);
                bulletIndent = line.Left;
                previousBottom = line.Bottom;
                previousLeft = line.Left;
                continue;
            }

            var gap = previousBottom is null ? 0 : previousBottom.Value - line.Bottom;
            var sameColumn = line.Left >= bulletIndent + BulletContinuationIndent &&
                             line.Left < bulletIndent + 48;
            if (bullet is not null && gap >= 0 && gap <= paragraphGap && sameColumn)
            {
                bullet = bullet + " " + text;
                previousBottom = line.Bottom;
                previousLeft = line.Left;
                continue;
            }

            FlushBullet();
            var jumpedColumn = previousLeft is not null && Math.Abs(line.Left - previousLeft.Value) > 80;
            if (paragraph.Count > 0 && (gap < 0 || gap > paragraphGap || jumpedColumn))
                FlushParagraph();

            paragraph.Add(text);
            previousBottom = line.Bottom;
            previousLeft = line.Left;
        }

        FlushBullet();
        FlushParagraph();
        return blocks;
    }

    private static bool StartsWithBullet(string text) =>
        text.StartsWith('•') ||
        text.StartsWith('●') ||
        text.StartsWith('▪') ||
        text.StartsWith('-') ||
        text.StartsWith('*');

    private static string StripBullet(string text)
    {
        var stripped = text.TrimStart('•', '●', '▪', '-', '*', ' ').Trim();
        return stripped.Length == 0 ? text : stripped;
    }

    private static string CollapseSpaces(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    private sealed class TextLine
    {
        private readonly List<GlyphWord> _words = new();

        public TextLine(GlyphWord first) => _words.Add(first);

        public double Left => _words.Min(w => w.Left);
        public double Right => _words.Max(w => w.Right);
        public double Bottom => _words[0].Bottom;
        public double Top => _words.Max(w => w.Top);
        public double Height => _words.Max(w => w.Height);

        public string Text => string.Join(" ", _words.OrderBy(w => w.Left).Select(w => w.Text));

        public void Add(GlyphWord word) => _words.Add(word);
    }

    private readonly record struct GlyphWord(string Text, PdfRectangle Box)
    {
        public double Left => Box.Left;
        public double Right => Box.Right;
        public double Bottom => Box.Bottom;
        public double Top => Box.Top;
        public double Height => Box.Height;
    }
}

internal readonly record struct LayoutBlock(string Tag, string Text);
