# Interview quick notes

## 30-second pitch

ChopDoc is a modular monolith: Angular submits a PDF job; a .NET API orchestrates convert → split → export → validate; every status is persisted with history. Conversion is PdfPig PDF→HTML, rule-based, no OCR. HTML is an intermediate — one conversion feeds HTML and DOCX exporters. Parts are sized against the exported format and validated before a job can be marked Completed.

## Questions they may ask

**Why not microservices?**  
One use case; interfaces already separate responsibilities. Microservice hop cost isn't justified yet. Evolution path is a background worker first.

**Why HTML as an intermediate rather than converting straight to DOCX?**  
Splitting and validation need something deterministic with markers. One conversion, one splitter, one validator, two exporters — adding a format is an `IOutputExporter`, not another pipeline.

**The limit is 2 MB — 2 MB of what?**  
The file that gets handed off. The splitter takes an `ExportedSizeProbe` and measures each candidate part with a real export, so `samples/large-over-2mb.pdf` needs two parts as HTML at a 2 MB limit but stays a single 40 KB part as DOCX. Export happens before validation, so validation checks the delivered bytes.

**Then why not split the exported file directly?**  
A DOCX is a zip; you cannot cut it in half. Boundaries come from HTML units, sizes come from the export.

**What does that cost?**  
A real export per candidate pack. For HTML the probe is free; for DOCX it is a small zip per pack. Correctness over speed at this size, and caching is in the backlog.

**How do you detect scanned PDFs?**  
No extractable text layer across the document → `ScannedDocumentException`. Known limit: a mostly-scanned PDF with one stray character passes. Per-page text density is the fix.

**How do you know nothing was lost when splitting?**  
Marker uniqueness alone only proves the parts are consistent with each other — that stays true if a page is dropped. So the converter reports the sections it produced and the validator asserts every one appears in the output, including as the `page-N#1` atoms an oversized page was broken into. Atom expansion keeps text that sits outside block elements for the same reason.

**What happens to an image PdfPig cannot decode?**  
PNG first, then JPEG raw-byte passthrough for DCTDecode. Anything left goes into the job history as a warning — visible, not silently dropped.

**How do images end up in the right place?**  
Layout blocks carry the vertical span of their lines, and each image is slotted in front of the first block starting at or below it — slotted into the existing block order, not sorted in by position, because the layout pass already handles two-column reading order.

**Failed vs NeedsReview?**  
Failed = cannot proceed under the rules. NeedsReview = processed but the output is not safe to accept. Completed requires validation to have passed.

**What would you change with more time?**  
Async worker, PDF exporter, content hashing below page level, cached export measurements, auth, EF migrations.

## Files to know cold

1. `DocumentJobService` — pipeline: convert → split → export → validate → persist  
2. `PdfDocumentConverter` — conversion rules, page markers, image passthrough  
3. `MarkedDocumentSplitter` — export-aware packing, atom expansion, unsplittable  
4. `DocumentOutputValidator` — structure/coverage on the intermediate, size on the exported parts  
5. `HtmlOutputExporter` — HTML and DOCX from the same intermediate, images in document order  
6. `DocumentJob` — status, history, and warning ownership  
7. `DocumentJobServiceTests` — proves the export-size rule end to end
