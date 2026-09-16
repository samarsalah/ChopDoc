using System.Text;
using ChopDoc.Domain.Models;
using ChopDoc.Infrastructure.Processing;

namespace ChopDoc.Tests;

public class DocumentOutputValidatorTests
{
    private readonly DocumentOutputValidator _sut = new();

    private static readonly string[] NoExpectedMarkers = Array.Empty<string>();

    [Fact]
    public void ValidateStructure_WhenSingleValidPart_Succeeds()
    {
        var html = Encoding.UTF8.GetBytes(Document(("page-1", "ok")));
        var parts = new[]
        {
            new SplitPartContent(1, 1, html, "doc.html", SplitPartContent.FullDocumentMarker)
        };

        var result = _sut.ValidateStructure(parts, new[] { "page-1" });

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void ValidateStructure_WhenPartCountMismatch_Fails()
    {
        var html = Encoding.UTF8.GetBytes(Document(("page-1", "a")));
        var parts = new[]
        {
            new SplitPartContent(1, 2, html, "a.html", "page-1")
        };

        var result = _sut.ValidateStructure(parts, NoExpectedMarkers);

        Assert.False(result.IsValid);
        Assert.Contains("Expected 2 parts", result.FailureReason);
    }

    [Fact]
    public void ValidateStructure_WhenMissingPartNumber_Fails()
    {
        var p1 = Encoding.UTF8.GetBytes(Document(("page-1", "a")));
        var p2 = Encoding.UTF8.GetBytes(Document(("page-2", "b")));
        var parts = new[]
        {
            new SplitPartContent(1, 2, p1, "a.html", "page-1"),
            new SplitPartContent(1, 2, p2, "b.html", "page-2") // duplicate 1, missing 2
        };

        var result = _sut.ValidateStructure(parts, NoExpectedMarkers);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate part number", result.FailureReason);
    }

    [Fact]
    public void ValidateStructure_WhenDuplicateMarkersAcrossParts_Fails()
    {
        var html = Encoding.UTF8.GetBytes(Document(("page-1", "a")));
        var parts = new[]
        {
            new SplitPartContent(1, 2, html, "a.html", "page-1"),
            new SplitPartContent(2, 2, html, "b.html", "page-1")
        };

        var result = _sut.ValidateStructure(parts, NoExpectedMarkers);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate content marker", result.FailureReason);
    }

    /// <summary>
    /// The parts below are internally consistent — correct sequence, no duplicates — but a source
    /// page never made it into the output. Without the coverage check this would pass.
    /// </summary>
    [Fact]
    public void ValidateStructure_WhenSourceSectionMissingFromOutput_Fails()
    {
        var p1 = Encoding.UTF8.GetBytes(Document(("page-1", "a")));
        var p2 = Encoding.UTF8.GetBytes(Document(("page-3", "c")));
        var parts = new[]
        {
            new SplitPartContent(1, 2, p1, "a.html", "page-1"),
            new SplitPartContent(2, 2, p2, "b.html", "page-3")
        };

        var result = _sut.ValidateStructure(parts, new[] { "page-1", "page-2", "page-3" });

        Assert.False(result.IsValid);
        Assert.Contains("'page-2' is missing", result.FailureReason);
    }

    /// <summary>An oversized page arrives as atoms, which still counts as covering that page.</summary>
    [Fact]
    public void ValidateStructure_WhenPageArrivesAsAtoms_CountsAsCovered()
    {
        var p1 = Encoding.UTF8.GetBytes(Document(("page-1#1", "first half")));
        var p2 = Encoding.UTF8.GetBytes(Document(("page-1#2", "second half")));
        var parts = new[]
        {
            new SplitPartContent(1, 2, p1, "a.html", "page-1#1"),
            new SplitPartContent(2, 2, p2, "b.html", "page-1#2")
        };

        var result = _sut.ValidateStructure(parts, new[] { "page-1" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStructure_WhenNoParts_Fails()
    {
        var result = _sut.ValidateStructure(Array.Empty<SplitPartContent>(), NoExpectedMarkers);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateExportedParts_WhenWithinLimit_Succeeds()
    {
        var parts = new[]
        {
            new ExportedPart(1, 2, new byte[400], "a.docx"),
            new ExportedPart(2, 2, new byte[300], "b.docx")
        };

        var result = _sut.ValidateExportedParts(parts, 500);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// The limit is enforced on the delivered file, so an exported part over the limit is a
    /// validation failure even when the intermediate it came from was under it.
    /// </summary>
    [Fact]
    public void ValidateExportedParts_WhenExportedPartExceedsLimit_Fails()
    {
        var parts = new[]
        {
            new ExportedPart(1, 1, new byte[900], "doc.docx")
        };

        var result = _sut.ValidateExportedParts(parts, 500);

        Assert.False(result.IsValid);
        Assert.Contains("exceeding limit", result.FailureReason);
    }

    [Fact]
    public void ValidateExportedParts_WhenPartIsEmpty_Fails()
    {
        var parts = new[]
        {
            new ExportedPart(1, 1, Array.Empty<byte>(), "doc.docx")
        };

        var result = _sut.ValidateExportedParts(parts, 500);

        Assert.False(result.IsValid);
        Assert.Contains("empty content", result.FailureReason);
    }

    private static string Document(params (string Marker, string Text)[] sections) =>
        "<!DOCTYPE html><html><body>"
        + string.Concat(sections.Select(s =>
            $"<section data-chopdoc-marker=\"{s.Marker}\"><p>{s.Text}</p></section>"))
        + "</body></html>";
}
