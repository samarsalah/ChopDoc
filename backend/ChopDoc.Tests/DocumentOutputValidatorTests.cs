using System.Text;
using ChopDoc.Domain.Models;
using ChopDoc.Infrastructure.Processing;

namespace ChopDoc.Tests;

public class DocumentOutputValidatorTests
{
    private readonly DocumentOutputValidator _sut = new();

    [Fact]
    public void Validate_WhenSingleValidPart_Succeeds()
    {
        var html = Encoding.UTF8.GetBytes("<html><body>ok</body></html>");
        var parts = new[]
        {
            new SplitPartContent(1, 1, html, "doc.html", "full-document")
        };

        var result = _sut.Validate(parts, html.LongLength + 10);

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Validate_WhenPartCountMismatch_Fails()
    {
        var html = Encoding.UTF8.GetBytes(Section("page-1", "a"));
        var parts = new[]
        {
            new SplitPartContent(1, 2, html, "a.html", "page-1")
        };

        var result = _sut.Validate(parts, 10_000);

        Assert.False(result.IsValid);
        Assert.Contains("Expected 2 parts", result.FailureReason);
    }

    [Fact]
    public void Validate_WhenMissingPartNumber_Fails()
    {
        var p1 = Encoding.UTF8.GetBytes(Section("page-1", "a"));
        var p2 = Encoding.UTF8.GetBytes(Section("page-2", "b"));
        var parts = new[]
        {
            new SplitPartContent(1, 2, p1, "a.html", "page-1"),
            new SplitPartContent(1, 2, p2, "b.html", "page-2") // duplicate 1, missing 2
        };

        var result = _sut.Validate(parts, 10_000);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate part number", result.FailureReason);
    }

    [Fact]
    public void Validate_WhenPartExceedsLimit_Fails()
    {
        var html = Encoding.UTF8.GetBytes(Section("page-1", new string('Z', 200)));
        var parts = new[]
        {
            new SplitPartContent(1, 1, html, "doc.html", "page-1")
        };

        var result = _sut.Validate(parts, 50);

        Assert.False(result.IsValid);
        Assert.Contains("exceeding limit", result.FailureReason);
    }

    [Fact]
    public void Validate_WhenDuplicateMarkersAcrossParts_Fails()
    {
        var html = Encoding.UTF8.GetBytes(Section("page-1", "a"));
        var parts = new[]
        {
            new SplitPartContent(1, 2, html, "a.html", "page-1"),
            new SplitPartContent(2, 2, html, "b.html", "page-1")
        };

        var result = _sut.Validate(parts, 10_000);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate content marker", result.FailureReason);
    }

    [Fact]
    public void Validate_WhenNoParts_Fails()
    {
        var result = _sut.Validate(Array.Empty<SplitPartContent>(), 1000);
        Assert.False(result.IsValid);
    }

    private static string Section(string marker, string text) =>
        $"<!DOCTYPE html><html><body><section data-chopdoc-marker=\"{marker}\"><p>{text}</p></section></body></html>";
}
