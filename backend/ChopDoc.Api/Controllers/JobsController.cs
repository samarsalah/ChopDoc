using ChopDoc.Application.DTOs;
using ChopDoc.Application.Services;
using ChopDoc.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ChopDoc.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController : ControllerBase
{
    private readonly IDocumentJobService _jobs;

    public JobsController(IDocumentJobService jobs)
    {
        _jobs = jobs;
    }

    /// <summary>Submit a PDF for conversion, optional splitting, and validation.</summary>
    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(JobDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobDetailDto>> Submit(
        [FromForm] IFormFile file,
        [FromForm] string outputFormat = "Html",
        [FromForm] double? sizeLimitMb = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { errorCode = "MISSING_FILE", message = "A PDF file is required." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _jobs.SubmitAsync(
                new SubmitJobRequest
                {
                    FileStream = stream,
                    FileName = file.FileName,
                    OutputFormat = outputFormat,
                    SizeLimitMb = sizeLimitMb
                },
                cancellationToken);

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errorCode = "INVALID_REQUEST", message = ex.Message });
        }
    }

    /// <summary>List all jobs (newest first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobSummaryDto>>> List(CancellationToken cancellationToken)
    {
        var jobs = await _jobs.ListAsync(cancellationToken);
        return Ok(jobs);
    }

    /// <summary>Get full job detail including parts and history.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetByIdAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    /// <summary>Download a generated output part.</summary>
    [HttpGet("{jobId:guid}/parts/{partId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPart(
        Guid jobId,
        Guid partId,
        CancellationToken cancellationToken)
    {
        var part = await _jobs.OpenPartAsync(jobId, partId, cancellationToken);
        if (part is null)
            return NotFound();

        return File(part.Value.Content, part.Value.ContentType, part.Value.FileName);
    }
}
