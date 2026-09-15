# Interview quick notes

## 30-second pitch

ChopDoc is a modular monolith: Angular submits a PDF job; a .NET API orchestrates convert → split → validate; every status is persisted with history. Conversion is PdfPig PDF→HTML (no OCR). Oversize HTML is split into ordered parts and validated before Completed.

## Questions they may ask

**Why not microservices?**  
One use case; interfaces already separate responsibilities. Microservice hop cost isn’t justified yet.

**Why HTML not DOCX?**  
Deterministic markers for split/validate depth. DOCX is a future `IDocumentConverter`.

**How do you detect scanned PDFs?**  
No extractable text layer across pages → `ScannedDocumentException`.

**How do you know nothing was lost when splitting?**  
Each page section has `data-chopdoc-marker`; validator checks sequence, totals, and marker uniqueness.

**What would you change with more time?**  
Async worker, DOCX converter, content hashing, auth, EF migrations.

## Files to know cold

1. `DocumentJobService` — pipeline  
2. `PdfToHtmlConverter` — conversion rules  
3. `HtmlDocumentSplitter` — size packing / unsplittable  
4. `DocumentOutputValidator` — gate before Completed  
5. `DocumentJob` — status + history ownership  
