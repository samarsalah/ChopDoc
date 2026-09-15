# Design decisions

## Why PDF → HTML?

- Assessment allows DOCX or structured HTML/text.  
- HTML makes **splitting and validation** rigorous (page markers, size packing, duplicate detection).  
- Still rule-based: PdfPig text extraction + images embedded as-is (no OCR).

## Why modular monolith?

- One business pipeline, one team, local demo.  
- SOLID via interfaces/projects without microservice operational cost.  
- Interviewers can ask “why not microservices?” — answer: premature distribution.

## Why SQLite + local disk?

- Requirement: simulate locally, no real external systems.  
- Zero install friction for the live demo.

## Failed vs NeedsReview

| Status | Meaning |
|--------|---------|
| **Failed** | Cannot proceed under rules (scanned, corrupted, unsupported format) |
| **NeedsReview** | Processed but output unsafe to accept (validation fail, unsplittable unit) |
| **Completed** | Converted, split if needed, **and** validated |

## CORS

Origins live in **appsettings**; middleware registration stays in **Program.cs** (config vs code).
