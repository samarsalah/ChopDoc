# How to test ChopDoc

## 1) Automated tests (must-run before interview)

```bash
cd backend
dotnet test
```

Expected: **all tests passed** (conversion, splitting, validation, job status).

What they prove:

- Text PDF → HTML with page markers  
- No text layer → `SCANNED_DOCUMENT`  
- Corrupted PDF → unsupported/corrupted  
- Split under / exact / over limit  
- Unsplittable section → exception  
- Validator catches missing/duplicate/oversized parts  

---

## 2) Manual demo (UI)

### Start

```bash
# Terminal A
cd backend/ChopDoc.Api
dotnet run --launch-profile http

# Terminal B
cd frontend/chopdoc-ui
npm start
```

- UI: http://localhost:4200  
- API: http://localhost:5105  

### Scenario checklist

| # | Action | Expected |
|---|--------|----------|
| 1 | Upload `samples/text-sample.pdf`, format HTML, limit 2 | **Completed**, 1 part, downloadable HTML |
| 2 | Upload `samples/multipage-text-sample.pdf`, limit **0.01** | **Completed** with **multiple parts** (`part N of M`) |
| 3 | Upload `samples/scanned-no-text-sample.pdf` | **Failed**, error `SCANNED_DOCUMENT`, visible in history |
| 4 | Upload a `.txt` renamed or non-PDF | **Failed** job persisted (`UNSUPPORTED_OR_CORRUPTED_INPUT`) |
| 5 | Submit with output format `Docx` (via API) | **Failed** job (`UNSUPPORTED_OUTPUT_FORMAT`) |
| 6 | Click a past job in history | Detail shows timeline + downloads when completed |

### Force splitting tip

In the UI set **Size limit (MB)** to `0.01` so multipage HTML exceeds the limit and creates sequenced parts.

---

## 3) Manual API (optional)

```bash
curl -F "file=@samples/text-sample.pdf" -F "outputFormat=Html" -F "sizeLimitMb=2" http://localhost:5105/api/jobs
curl http://localhost:5105/api/jobs
curl http://localhost:5105/api/jobs/{id}
```

Health:

```bash
curl http://localhost:5105/api/health
```

---

## 4) Interview readiness

1. Run `dotnet test` once the morning of the interview.  
2. Keep both API + UI running before the call.  
3. Have the three sample PDFs ready.  
4. Be ready to open `DocumentJobService`, `PdfToHtmlConverter`, `HtmlDocumentSplitter`, `DocumentOutputValidator` and walk the flow.
