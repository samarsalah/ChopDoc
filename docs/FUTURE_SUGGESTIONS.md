# Future suggestions & improvement points

Living backlog for ChopDoc — useful for the assessment “with more time” note and for continuing the product after submission.

Priority legend: **P0** = high value / interview-ready stretch · **P1** = strong next · **P2** = later / nice-to-have

---

## Pipeline & formats

| ID | Suggestion | Priority | Notes |
|----|------------|----------|--------|
| F1 | **PDF export** (HTML → PDF via QuestPDF or PdfSharp) | P0 | Same HTML-first path as DOCX; page-based parts |
| F2 | **Richer DOCX** (styles, tables, better image sizing) | P1 | Open XML already in place |
| F3 | ~~Apply size limit on final export bytes~~ | **Done** | `ExportedSizeProbe`; packing and validation both measure the exported artifact |
| F4 | Real **image-only scanned** sample PDF (embedded image, no text) | P1 | Current sample = empty text layer (same business rule) |
| F5 | Content **hashing** across parts for stronger validation | P1 | Coverage is page-level today; hashing would catch sub-page loss |
| F6 | More exporters (RTF/XML/JSON) behind `IOutputExporter` | P2 | `OutputFormat` already names them; requesting one over the API is a **Failed** job, so the path is proven |
| F7 | **Cache export measurements** while packing | P1 | Each candidate pack is a real export; fine at this size, wasteful for large DOCX |
| F8 | **Per-page text density** for scanned detection | P1 | Today one stray character makes a scanned PDF look convertible |
| F9 | Wider image passthrough (JPX/CMYK, not just PNG + JPEG) | P2 | Unsupported encodings currently surface as job warnings |

---

## Architecture & scale

| ID | Suggestion | Priority | Notes |
|----|------------|----------|--------|
| A1 | **Background worker / queue** for conversion jobs | P0 | API returns `Queued`; UI polls status |
| A2 | Extract conversion to a **separate worker process** | P1 | Only if CPU/load requires it — not fake microservices |
| A3 | EF Core **migrations** instead of `EnsureCreated` | P1 | Safer for evolving schema |
| A4 | Outbox / audit events for job lifecycle | P2 | Enterprise handoff story |

---

## API & security

| ID | Suggestion | Priority | Notes |
|----|------------|----------|--------|
| S1 | AuthN/AuthZ (JWT or API key) on `/api/jobs` | P1 | Not required by assessment |
| S2 | Virus / file-type sniffing beyond extension | P1 | Magic-byte PDF check |
| S3 | Rate limiting & max upload quotas | P2 | Protect demo/prod |
| S4 | OpenAPI examples for each output format | P2 | Swagger already at `/swagger` |

---

## Frontend

| ID | Suggestion | Priority | Notes |
|----|------------|----------|--------|
| U1 | Poll job detail while status is in-progress (async era) | P0 | Pairs with A1 |
| U2 | Filter/search job history by status / format | P1 | |
| U3 | Inline preview of HTML parts | P2 | |
| U4 | Drag-and-drop upload | P2 | |

---

## Quality & ops

| ID | Suggestion | Priority | Notes |
|----|------------|----------|--------|
| Q1 | Integration tests hitting API + SQLite | P0 | Complement unit tests |
| Q2 | CI (GitHub Actions): `dotnet test` + Angular build | P0 | |
| Q3 | Structured logging + correlation id per job | P1 | |
| Q4 | Metrics (job duration, fail rates by error code) | P2 | |
| Q5 | Docker Compose (API + UI) for one-command demo | P1 | |

---

## Interview / product talking points

- Keep defending **HTML as intermediate**: one convert, one split/validate, many exporters.
- The limit is enforced on the **delivered artifact**, not the intermediate — boundaries come from HTML units, sizes come from a real export.
- Validation proves **coverage**, not just internal consistency: every source page has to appear in the output.
- Prefer **modular monolith → worker** before microservices.
- **Failed** vs **NeedsReview** stays intentional: rules violation vs output integrity concern.
- Non-fatal problems (undecodable image) become **history warnings**, not silent skips.
- Depth on edges (scanned, unsplittable, exact limit) beats adding many shallow formats.

---

## How to use this file

1. Before the interview: skim **P0** items as “what I’d do next.”  
2. After submission: pick one vertical (e.g. async jobs + PDF export) and implement end-to-end.  
3. When adding a feature, tick/move the row here or link a PR.

*Last focused areas shipped:* export-aware splitting and validation (F3), page-level coverage validation, JPEG image passthrough with history warnings, images placed inline with the text they sit between, HTML-first pipeline, DOCX via Open XML, Swagger at `/swagger`.
