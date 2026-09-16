# Requirements traceability

Mapped to DigiArenas *Senior Full Stack Technical Assessment*.

| Req | Requirement | Status | Where |
|------|-------------|--------|--------|
| 4.1 | Accept PDF + requested output format | Done | `JobsController` + Angular submit form |
| 4.1 | Sample PDFs (text + scanned/no-text) | Done | `samples/` |
| 4.2 | Rule-based conversion (no OCR/AI) | Done | `PdfDocumentConverter` (PdfPig) |
| 4.2 | Text faithful; images copied as-is | Done | HTML text + embedded `data:` images (PNG, with JPEG passthrough) |
| 4.2 | Scanned / no text layer → exception | Done | `ScannedDocumentException` → job **Failed** |
| 4.2 | State output choice in README | Done | PDF → **HTML** intermediate → HTML / DOCX |
| 4.3 | Configurable size limit (default 2 MB) | Done | `DocumentProcessing:DefaultSizeLimitMb` + UI override |
| 4.3 | Limit applies to the delivered artifact | Done | `ExportedSizeProbe` — packing measures a real export, not the intermediate |
| 4.3 | Ordered parts (`part N of M`) | Done | `DocumentPart.SequenceLabel` + filenames |
| 4.3 | Edge: exact limit / unsplittable / empty | Done | Splitter + unit tests |
| 4.4 | Validate before complete | Done | `DocumentOutputValidator`, run before anything is written to storage |
| 4.4 | Right number of parts | Done | `ValidateSequence` on both the intermediate and the exported parts |
| 4.4 | Nothing lost or duplicated | Done | `ValidateCoverage` asserts every `ConversionResult.PageMarkers` entry reaches the output |
| 4.4 | Validation fail → review, not success | Done | **NeedsReview** |
| 4.5 | Unsupported/corrupted input | Done | Domain exception + job history |
| 4.5 | Unsupported requested format | Done | Persisted job **Failed** (`UNSUPPORTED_OUTPUT_FORMAT`) |
| 4.5 | Scanned/image-only | Done | No extractable text layer |
| 4.5 | Exceptions visible in history | Done | `JobHistoryEntry` on every transition, plus `DocumentJob.AddWarning` for non-fatal issues |
| 4.6 | Persist every request | Done | Job row created on submit (including intake failures) |
| 4.6 | Angular submit + history | Done | `JobsPageComponent` |
| 5 | Separation of concerns | Done | Api / Application / Domain / Infrastructure |
| 5 | Deliberate error handling | Done | Typed `DomainException` + status mapping; cancellation and unexpected errors handled separately |
| 5 | Automated tests on core logic | Done | 38 tests in `ChopDoc.Tests` |
| 8 | README setup + assumptions + more time | Done | Root `README.md` |

## Intentional trade-offs

| Topic | Choice | Why |
|-------|--------|-----|
| HTML as intermediate | Convert once, export many | Split and validate logic is written against one deterministic structure with markers; adding a format is a new `IOutputExporter`, not a new pipeline |
| Split boundaries | Chosen on HTML units, sized by real export | Splitting a DOCX (a zip) directly is impractical; measuring each candidate pack keeps the limit honest without giving up the single pipeline |
| Packing cost | A real export per candidate pack | Correctness over speed at this size; for HTML the probe is free, and caching is in the backlog |
| Processing | Synchronous API | Reliable live demo; async noted as future work |
| Persistence | SQLite + local disk | Matches "simulate locally" constraint |
| Scanned sample | PDF with empty text layer | Exercises the same rule as image-only (no extractable text) |
| Coverage granularity | Page-level | Markers exist per page; sub-page loss would need content hashing (backlog item F5) |

## Where the limit is enforced

Requirement 4.3 talks about the *converted output*, which is the file handed off — not the HTML it was derived from. Two things follow, and both are tested:

1. `MarkedDocumentSplitter` takes an `ExportedSizeProbe` and measures every candidate part in the requested format. A document whose HTML exceeds 2 MB but whose plain-text export does not stays a single part.
2. `DocumentJobService` exports into memory *before* validating, so `ValidateExportedParts` checks the bytes that will actually be written, and a failure leaves nothing behind in storage.
