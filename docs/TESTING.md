# How to test ChopDoc

## 1) Automated tests (must-run before interview)

```bash
cd backend
dotnet test
```

Expected: **31 tests passed** (conversion, splitting, validation, job status, pipeline).

What they prove:

- Text PDF → HTML with page markers  
- No text layer → `SCANNED_DOCUMENT`  
- Corrupted PDF → unsupported/corrupted  
- Split under / exact / over limit  
- Splitting is sized by the **exported** format: a large HTML intermediate that exports small stays one part, and a format that inflates produces more parts  
- Atom expansion keeps text that sits outside block elements  
- Unsplittable section → exception  
- Validator catches missing/duplicate/oversized parts  
- Validator catches a **missing source page** even when the parts are internally consistent  
- End-to-end pipeline: same PDF gives one plain-text part and several HTML parts at the same limit  

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
| 5 | Submit with output format `Rtf` (via API) | **Failed** job (`UNSUPPORTED_OUTPUT_FORMAT`) |
| 6 | Click a past job in history | Detail shows timeline + downloads when completed |
| 7 | Same multipage PDF at limit `0.01`, once as **HTML** and once as **Plain Text** | HTML splits into several parts; plain text needs fewer or one — the limit follows the delivered format |

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
4. Be ready to open `DocumentJobService`, `PdfDocumentConverter`, `MarkedDocumentSplitter`, `DocumentOutputValidator` and walk the flow.
