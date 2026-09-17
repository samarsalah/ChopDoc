# Design decisions

## Why HTML as the intermediate?

- The assessment allows DOCX or a structured HTML/text target. We deliver both, from a single conversion.
- HTML gives splitting and validation something deterministic to work with: page sections carry a `data-chopdoc-marker`, so parts can be packed, counted, and checked for loss or duplication.
- Adding a format is a new `IOutputExporter`, not a second pipeline. DOCX is an exporter over the same HTML.
- Still rule-based: PdfPig text extraction plus images copied across untouched. No OCR.

## Where the size limit is enforced

The limit belongs to the artifact that gets handed off, so measuring the HTML intermediate would be measuring the wrong thing — DOCX is usually smaller, and judging by the HTML would split documents that would have fit in one part. `samples/large-over-2mb.pdf` is the case in point: at a 2 MB limit it needs two parts as HTML and stays a single 40 KB part as DOCX.

- `MarkedDocumentSplitter` receives an `ExportedSizeProbe` and sizes every candidate part with a real export.
- `DocumentJobService` exports into memory, then validates, then writes. Validation therefore runs against the delivered bytes, and a validation failure leaves nothing in storage.

The cost is a real export per candidate pack. That is acceptable at this document size and it is honest about what it measures; caching the measurements is in the backlog.

## Why split HTML units rather than the exported file?

A DOCX is a zip — you cannot meaningfully cut it in half. So boundaries are chosen on HTML units (page sections, then smaller atoms) while the *size* of each candidate is measured on the export. Each part is a standalone, valid file in the requested format.

## How we know nothing was lost

Marker uniqueness alone only proves the parts are internally consistent, which stays true even if the splitter drops a page. So the converter reports the sections it produced (`ConversionResult.PageMarkers`) and `ValidateCoverage` asserts every one of them appears in the output — as itself, or as the `page-N#1`, `page-N#2` atoms an oversized page was broken into.

Atom expansion is also lossless by construction: text between block elements becomes an atom of its own, and only gaps that carry no text once tags are stripped (list wrappers, whitespace) are discarded.

## Why modular monolith?

- One business pipeline, one team, local demo.
- SOLID via interfaces/projects without microservice operational cost.
- Interviewers can ask "why not microservices?" — answer: premature distribution.

## Why SQLite + local disk?

- Requirement: simulate locally, no real external systems.
- Zero install friction for the live demo.

## Failed vs NeedsReview

| Status | Meaning |
|--------|---------|
| **Failed** | Cannot proceed under rules (scanned, corrupted, unsupported format, cancelled) |
| **NeedsReview** | Processed but output unsafe to accept (validation fail, unsplittable unit) |
| **Completed** | Converted, split if needed, exported, **and** validated |

## Where images go in the output

Emitting a page's text and then its images is easier, but it reorders the document: a page laid out text/image/text comes out text/text/image, and every exporter inherits that. So layout blocks carry the vertical span of the lines they were built from, and each image is slotted in front of the first block that starts at or below it.

Images are slotted into the existing block order rather than sorted in with the blocks by position, because the layout pass already reorders lines to get two-column reading order right and a position sort would undo that.

## Warnings vs exceptions

An image in an encoding we cannot copy across is not a reason to fail the job, but it is also not something to swallow. `DocumentJob.AddWarning` writes it into the job history at the current status, so it is visible in the UI without changing the outcome.

## Error handling boundaries

- Malformed requests (missing file, size limit ≤ 0) are rejected at the controller with **HTTP 400** and no job record.
- Well-formed requests that cannot be processed become **Failed** jobs, so they show up in history.
- Cancellation is recorded rather than left mid-pipeline, saved on `CancellationToken.None` because the request token is already dead.
- The catch-all records the exception type, not `ex.Message` — internals belong in logs, not in a field the API hands to clients.

## CORS

Origins live in **appsettings**; middleware registration stays in **Program.cs** (config vs code).
