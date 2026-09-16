using System.Text;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Domain.Exceptions;
using ChopDoc.Domain.Models;
using ChopDoc.Infrastructure.Processing;

namespace ChopDoc.Tests;

public class MarkedDocumentSplitterTests
{
    private readonly MarkedDocumentSplitter _sut = new();

    /// <summary>HTML output is exported unchanged, so its exported size is its own size.</summary>
    private static readonly ExportedSizeProbe AsHtml = content => content.LongLength;

    [Fact]
    public void SplitIfNeeded_WhenUnderLimit_ReturnsSinglePart()
    {
        var content = Encoding.UTF8.GetBytes(BuildHtml(("page-1", "Hello")));
        var limit = content.LongLength + 10;

        var parts = _sut.SplitIfNeeded(content, "doc", ".html", limit, AsHtml);

        Assert.Single(parts);
        Assert.Equal(1, parts[0].PartNumber);
        Assert.Equal(1, parts[0].TotalParts);
        Assert.Equal(SplitPartContent.FullDocumentMarker, parts[0].ContentMarker);
        Assert.Equal("doc.html", parts[0].FileName);
    }

    [Fact]
    public void SplitIfNeeded_WhenExactlyAtLimit_ReturnsSinglePart()
    {
        var content = Encoding.UTF8.GetBytes(BuildHtml(("page-1", "Exact")));
        var limit = content.LongLength;

        var parts = _sut.SplitIfNeeded(content, "doc", "html", limit, AsHtml);

        Assert.Single(parts);
        Assert.Equal(SplitPartContent.FullDocumentMarker, parts[0].ContentMarker);
    }

    [Fact]
    public void SplitIfNeeded_WhenOverLimit_SplitsIntoOrderedParts()
    {
        var page1 = ("page-1", new string('A', 400));
        var page2 = ("page-2", new string('B', 400));
        var page3 = ("page-3", new string('C', 400));
        var content = Encoding.UTF8.GetBytes(BuildHtml(page1, page2, page3));

        // Force roughly one section per part.
        var oneSection = Encoding.UTF8.GetByteCount(Wrap(Section(page1.Item1, page1.Item2)));
        var limit = oneSection + 50;

        var parts = _sut.SplitIfNeeded(content, "report", ".html", limit, AsHtml);

        Assert.True(parts.Count >= 2);
        Assert.Equal(parts.Count, parts[0].TotalParts);
        Assert.Equal(Enumerable.Range(1, parts.Count), parts.Select(p => p.PartNumber));
        Assert.All(parts, p => Assert.True(p.Content.LongLength <= limit));
        Assert.Contains("part1-of-", parts[0].FileName);
    }

    /// <summary>
    /// The limit applies to the delivered artifact. A large HTML intermediate that exports to a
    /// small file (plain text, DOCX) must not be split.
    /// </summary>
    [Fact]
    public void SplitIfNeeded_WhenExportedOutputFitsLimit_DoesNotSplit()
    {
        var content = Encoding.UTF8.GetBytes(BuildHtml(
            ("page-1", new string('A', 4000)),
            ("page-2", new string('B', 4000))));
        var limit = 1000;
        Assert.True(content.LongLength > limit);

        ExportedSizeProbe exportsSmall = _ => 200;

        var parts = _sut.SplitIfNeeded(content, "doc", ".html", limit, exportsSmall);

        Assert.Single(parts);
        Assert.Equal(SplitPartContent.FullDocumentMarker, parts[0].ContentMarker);
    }

    /// <summary>The reverse case: a format that inflates has to drive more parts, not fewer.</summary>
    [Fact]
    public void SplitIfNeeded_WhenExportInflatesContent_PacksAgainstExportedSize()
    {
        var pages = Enumerable.Range(1, 12)
            .Select(i => ($"page-{i}", new string('A', 300)))
            .ToArray();
        var content = Encoding.UTF8.GetBytes(BuildHtml(pages));
        const long limit = 3500;
        const int inflation = 6;

        ExportedSizeProbe exportsLarger = html => html.LongLength * inflation;

        var asHtml = _sut.SplitIfNeeded(content, "doc", ".html", limit, AsHtml);
        var inflated = _sut.SplitIfNeeded(content, "doc", ".html", limit, exportsLarger);

        Assert.True(inflated.Count > asHtml.Count);
        Assert.All(inflated, p => Assert.True(p.Content.LongLength * inflation <= limit));
    }

    [Fact]
    public void SplitIfNeeded_WhenPageExceedsLimit_SplitsIntoHtmlAtoms()
    {
        var blockA = new string('A', 900);
        var blockB = new string('B', 900);
        var section =
            $"<section data-chopdoc-marker=\"page-1\"><h2>Page 1</h2><p>{blockA}</p><p>{blockB}</p></section>";
        var content = Encoding.UTF8.GetBytes(Wrap(section));

        var oneAtomWrapped = Encoding.UTF8.GetByteCount(
            Wrap($"<section data-chopdoc-marker=\"page-1#1\"><p>{blockA}</p></section>"));
        var limit = oneAtomWrapped + 80;
        Assert.True(content.LongLength > limit);

        var parts = _sut.SplitIfNeeded(content, "doc", ".html", limit, AsHtml);

        Assert.True(parts.Count >= 2);
        Assert.All(parts, p => Assert.True(p.Content.LongLength <= limit));
        Assert.Contains("page-1#", string.Join(';', parts.Select(p => p.ContentMarker)));
    }

    /// <summary>
    /// Atom expansion must not quietly drop text that sits outside a block element, otherwise
    /// validation would pass on output that lost content.
    /// </summary>
    [Fact]
    public void SplitIfNeeded_WhenSplittingIntoAtoms_KeepsTextOutsideBlockElements()
    {
        const string stray = "STRAY-TEXT-OUTSIDE-A-BLOCK";
        var blockA = new string('A', 900);
        var blockB = new string('B', 900);
        var section =
            $"<section data-chopdoc-marker=\"page-1\"><p>{blockA}</p>{stray}<ul><li>{blockB}</li></ul></section>";
        var content = Encoding.UTF8.GetBytes(Wrap(section));
        var limit = Encoding.UTF8.GetByteCount(Wrap($"<section data-chopdoc-marker=\"page-1#1\"><p>{blockA}</p></section>")) + 80;

        var parts = _sut.SplitIfNeeded(content, "doc", ".html", limit, AsHtml);

        var allText = string.Concat(parts.Select(p => Encoding.UTF8.GetString(p.Content)));
        Assert.Contains(stray, allText);
        Assert.Contains(blockA, allText);
        Assert.Contains(blockB, allText);
    }

    [Fact]
    public void SplitIfNeeded_WhenSingleSectionExceedsLimit_ThrowsUnsplittable()
    {
        // One atomic <p> larger than the limit cannot be split further.
        var huge = ("page-1", new string('X', 5000));
        var content = Encoding.UTF8.GetBytes(BuildHtml(huge));
        var limit = 500;

        var ex = Assert.Throws<UnsplittableContentException>(() =>
            _sut.SplitIfNeeded(content, "huge", ".html", limit, AsHtml));

        Assert.Equal("UNSPLITTABLE_CONTENT", ex.ErrorCode);
    }

    [Fact]
    public void SplitIfNeeded_WhenEmpty_Throws()
    {
        Assert.Throws<UnsupportedOrCorruptedDocumentException>(() =>
            _sut.SplitIfNeeded(Array.Empty<byte>(), "empty", ".html", 1024, AsHtml));
    }

    private static string BuildHtml(params (string Marker, string Text)[] pages) =>
        Wrap(string.Concat(pages.Select(p => Section(p.Marker, p.Text))));

    private static string Section(string marker, string text) =>
        $"<section data-chopdoc-marker=\"{marker}\"><p>{text}</p></section>";

    private static string Wrap(string body) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /></head><body>" + body + "</body></html>";
}
