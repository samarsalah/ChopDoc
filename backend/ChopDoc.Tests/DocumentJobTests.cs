using ChopDoc.Domain.Entities;
using ChopDoc.Domain.Enums;
using ChopDoc.Domain.Exceptions;

namespace ChopDoc.Tests;

public class DocumentJobTests
{
    [Fact]
    public void MarkFailed_RecordsErrorAndHistory()
    {
        var job = new DocumentJob("a.pdf", "sources/a.pdf", OutputFormat.Html, 2_000_000);

        job.MarkConverting();
        job.MarkFailed(new ScannedDocumentException());

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("SCANNED_DOCUMENT", job.ErrorCode);
        Assert.Contains(job.History, h => h.Status == JobStatus.Failed);
    }

    [Fact]
    public void MarkNeedsReview_DoesNotMarkCompleted()
    {
        var job = new DocumentJob("a.pdf", "sources/a.pdf", OutputFormat.Html, 2_000_000);

        job.MarkNeedsReview(new UnsplittableContentException("too big"));

        Assert.Equal(JobStatus.NeedsReview, job.Status);
        Assert.Equal("UNSPLITTABLE_CONTENT", job.ErrorCode);
    }
}
