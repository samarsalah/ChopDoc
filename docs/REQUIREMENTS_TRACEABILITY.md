# Requirements traceability

Mapped to DigiArenas *Senior Full Stack Technical Assessment*.

| Req | Requirement | Status | Where |
|------|-------------|--------|--------|
| 4.1 | Accept PDF + requested output format | Done | `JobsController` + Angular submit form |
| 4.1 | Sample PDFs (text + scanned/no-text) | Done | `samples/` |
| 4.2 | Rule-based conversion (no OCR/AI) | Done | `PdfToHtmlConverter` (PdfPig) |
| 4.2 | Text faithful; images copied as-is | Done | HTML text + embedded `data:` images |
| 4.2 | Scanned / no text layer → exception | Done | `ScannedDocumentException` → job **Failed** |
| 4.2 | State output choice in README | Done | PDF → **HTML** |
| 4.3 | Configurable size limit (default 2 MB) | Done | `DocumentProcessing:DefaultSizeLimitMb` + UI override |
| 4.3 | Ordered parts (`part N of M`) | Done | `DocumentPart.SequenceLabel` + filenames |
| 4.3 | Edge: exact limit / unsplittable / empty | Done | Splitter + unit tests |
| 4.4 | Validate before complete | Done | `DocumentOutputValidator` |
| 4.4 | Validation fail → review, not success | Done | **NeedsReview** |
| 4.5 | Unsupported/corrupted input | Done | Domain exception + job history |
| 4.5 | Unsupported requested format | Done | Persisted job **Failed** (`UNSUPPORTED_OUTPUT_FORMAT`) |
| 4.5 | Scanned/image-only | Done | No extractable text layer |
| 4.5 | Exceptions visible in history | Done | `JobHistoryEntry` on every transition |
| 4.6 | Persist every request | Done | Job row created on submit (including intake failures) |
| 4.6 | Angular submit + history | Done | `JobsPageComponent` |
| 5 | Separation of concerns | Done | Api / Application / Domain / Infrastructure |
| 5 | Deliberate error handling | Done | Typed `DomainException` + status mapping |
| 5 | Automated tests on core logic | Done | 16 tests in `ChopDoc.Tests` |
| 8 | README setup + assumptions + more time | Done | Root `README.md` |

## Intentional trade-offs

| Topic | Choice | Why |
|-------|--------|-----|
| Output format | HTML (not DOCX) | Deterministic split/validate; easy to defend markers |
| Processing | Synchronous API | Reliable live demo; async noted as future work |
| Persistence | SQLite + local disk | Matches “simulate locally” constraint |
| Scanned sample | PDF with empty text layer | Exercises the same rule as image-only (no extractable text) |

## Small improvements applied after re-check

1. **Persist intake failures** (non-PDF / unsupported format) as Failed jobs with history — closer to “every request” tracking.  
2. Added **docs/** (architecture diagram, testing guide, decisions, interview notes).
