# ChopDoc

Document Conversion & Splitting Service — DigiArenas Senior Full Stack assessment.

**Stack:** Angular 17 + .NET 8 (modular monolith)  
**Conversion target:** PDF → **HTML** (canonical intermediate), then export to **HTML** or **DOCX**.

---

## Solution layout

```
ChopDoc/
├── backend/
│   ├── ChopDoc.Api              # HTTP controllers, CORS, startup
│   ├── ChopDoc.Application      # Job orchestration (use case)
│   ├── ChopDoc.Domain           # Entities, exceptions, abstractions
│   ├── ChopDoc.Infrastructure   # PdfPig, EF Core SQLite, file storage
│   └── ChopDoc.Tests            # Conversion / split / validate tests
├── frontend/chopdoc-ui          # Angular submit + history UI
└── samples/                     # Sample PDFs for manual testing
```

**Dependency direction:** Api → Application → Domain ← Infrastructure

---

## Docs

See [`docs/`](./docs/README.md) for architecture diagram, requirements traceability, testing guide, and interview notes.

---

## Prerequisites

- .NET 8 SDK
- Node.js 18+ (20 recommended)
- Angular CLI optional (`npx` works)

---

## Run — backend

```bash
cd backend/ChopDoc.Api
dotnet run --launch-profile http
```

API: **http://localhost:5105**  
Swagger UI: **http://localhost:5105/swagger** (also opens from `/`)

On startup the app creates SQLite DB + storage under `ChopDoc.Api/App_Data/`.

### Main endpoints

| Method | URL | Body |
|--------|-----|------|
| POST | `/api/jobs` | `multipart/form-data`: `file`, `outputFormat` (`Html` \| `Docx`), optional `sizeLimitMb` |
| GET | `/api/jobs` | Job list |
| GET | `/api/jobs/{id}` | Detail + history + parts |
| GET | `/api/jobs/{jobId}/parts/{partId}` | Download part |

---

## Run — frontend

```bash
cd frontend/chopdoc-ui
npm install
npm start
```

UI: **http://localhost:4200**  
CORS allows this origin via `appsettings.Development.json`.

---

## Run — tests

```bash
cd backend
dotnet test
```

38 tests covering conversion (text / scanned / corrupted / image placement), splitting (under / exact / over / unsplittable / empty / export-size-aware), validation (sequence, duplicates, source coverage, exported size), job status transitions, and the pipeline end to end.

---

## Sample PDFs

| File | Expected behaviour |
|------|--------------------|
| `samples/text-with-images.pdf` | Converts successfully (one page of text plus two embedded images) |
| `samples/scanned-no-text-sample.pdf` | Fails with `SCANNED_DOCUMENT` (image only, no text layer) |
| `samples/large-over-2mb.pdf` | Larger than 2 MB; splits at the default 2 MB limit |

Regenerate the large sample:

```bash
cd backend/tools/GenerateSamples
dotnet run -- ../../../samples
```

---

## How the pipeline works

```
Queued → Converting → Splitting → Validating → Completed
                              ↘ Failed / NeedsReview
```

1. **Convert (PdfPig):** extract text + embed images as-is into HTML page sections (`data-chopdoc-marker`). Text blocks carry the vertical span of their lines, so each image is placed between the blocks it sits between on the page rather than after all of them. No OCR. No text layer → `ScannedDocumentException`. Images PdfPig cannot re-encode are recorded as a warning in the job history rather than dropped quietly.
2. **Split:** every size decision is measured **in the requested output format**, not in the HTML intermediate — the limit applies to the file that gets handed off. If the exported output fits the limit → one part; otherwise page sections are packed into ordered parts (`part N of M`), each pack verified by a real export. An oversized page is broken into smaller HTML atoms first; a single atom over the limit → `UnsplittableContentException` → **NeedsReview**.
3. **Export then validate:** parts are exported in memory before validation, so validation sees the delivered artifacts. Structure and completeness are checked on the intermediate (where markers live): sequence, no duplicates, and **every source page present in the output**. Size is checked on the exported parts. Failure → **NeedsReview** (never marked Completed), and nothing is written to storage.
4. **Persist:** only after validation passes. Every status change and warning is stored in job history (SQLite).

---

## Configuration

`backend/ChopDoc.Api/appsettings*.json`:

- `Cors:AllowedOrigins` — frontend origins (config-driven)
- `ConnectionStrings:ChopDoc` — SQLite path
- `Storage:RootPath` — uploaded/generated files
- `DocumentProcessing:DefaultSizeLimitMb` — default **2**

---

## Assumptions

- Input format for this version is **PDF only** (non-PDF uploads are still persisted as **Failed** jobs).
- **HTML is an intermediate, not the deliverable.** One conversion feeds the HTML and DOCX exporters behind `IOutputExporter`, so splitting and validation logic is written once.
- Requests that are malformed at the edge (no file, size limit ≤ 0) are rejected with **HTTP 400** and no job record. Requests that are well-formed but cannot be processed (non-PDF, unsupported format, scanned, corrupted) are persisted as **Failed** jobs so they appear in history.
- Processing is **synchronous** inside the API request for a reliable live demo.
- “Scanned” is detected as **no extractable text layer** across the whole document (sample `scanned-no-text-sample.pdf` exercises that rule).
- Local SQLite + disk storage simulate the handoff pipeline without external systems.

---

## What I’d do with more time

See the full backlog in [`docs/FUTURE_SUGGESTIONS.md`](./docs/FUTURE_SUGGESTIONS.md). Highlights:

- Background worker / queue for large files  
- PDF export exporter (same HTML-first pipeline)  
- Content hashing to strengthen validation below page level  
- Cache export measurements during packing (each candidate pack is currently a real export)  
- Per-page text density instead of whole-document text presence for scanned detection  
- AuthN/AuthZ and stronger audit trail  
- EF migrations instead of `EnsureCreated`  
- Richer Angular UX (filters, polling for async jobs)

---

## Architecture note (interview)

This is a **modular monolith**, not microservices: convert / split / validate are separate **domain services** behind interfaces (SOLID), running in one deployable. That keeps demo reliability high while preserving clear boundaries and testability.
