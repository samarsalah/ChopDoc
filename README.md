# ChopDoc

Document Conversion & Splitting Service — DigiArenas Senior Full Stack assessment.

**Stack:** Angular 17 + .NET 8 (modular monolith)  
**Conversion target:** PDF → **HTML** (canonical intermediate), then export to HTML / Plain Text / Markdown / **DOCX**

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
| POST | `/api/jobs` | `multipart/form-data`: `file`, `outputFormat` (Html), optional `sizeLimitMb` |
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

Covers conversion (text / scanned / corrupted), splitting (under / exact / over / unsplittable / empty), validation, and job status transitions.

---

## Sample PDFs

| File | Expected behaviour |
|------|--------------------|
| `samples/text-sample.pdf` | Converts successfully |
| `samples/multipage-text-sample.pdf` | Converts; may split if size limit is low |
| `samples/scanned-no-text-sample.pdf` | Fails with `SCANNED_DOCUMENT` (no text layer) |

Regenerate samples:

```bash
cd backend/tools/GenerateSamples
dotnet run -- ../../../samples
```

Tip: set size limit to `0.01` MB in the UI to force splitting on multipage output.

---

## How the pipeline works

```
Queued → Converting → Splitting → Validating → Completed
                              ↘ Failed / NeedsReview
```

1. **Convert (PdfPig):** extract text + embed images as-is into HTML page sections (`data-chopdoc-marker`). No OCR. No text layer → `ScannedDocumentException`.
2. **Split:** if output ≤ limit → one part; else pack page sections into ordered parts (`part N of M`). One section larger than limit → `UnsplittableContentException` → **NeedsReview**.
3. **Validate:** part count, sequence, markers, sizes. Failure → **NeedsReview** (never marked Completed).
4. **Persist:** every status change is stored in job history (SQLite).

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
- Output format implemented is **HTML** (structured, deterministic, easy to split/validate). DOCX was deferred as a future format behind `IDocumentConverter`.
- Unsupported output formats are persisted as **Failed** jobs (not only HTTP 400).
- Processing is **synchronous** inside the API request for a reliable live demo.
- “Scanned” is detected as **no extractable text layer** (sample `scanned-no-text-sample.pdf` exercises that rule).
- Local SQLite + disk storage simulate the handoff pipeline without external systems.

---

## What I’d do with more time

- Background worker / queue for large files  
- Additional converters (e.g. PDF → DOCX) via Open/Closed principle  
- Content hashing to strengthen validation  
- AuthN/AuthZ and stronger audit trail  
- EF migrations instead of `EnsureCreated`  
- Richer Angular UX (filters, polling for async jobs)

---

## Architecture note (interview)

This is a **modular monolith**, not microservices: convert / split / validate are separate **domain services** behind interfaces (SOLID), running in one deployable. That keeps demo reliability high while preserving clear boundaries and testability.
