# ADR-0005: Dual OCR Strategy (PdfPig + Tesseract)

## Status
Accepted, with implementation notes (October 2026)

## Context
Documents arrive in two fundamentally different formats:

1. **Digital PDFs** — Created by software (Word, TMS exports, e-signatures). Contain an embedded text layer that can be extracted directly. High quality, fast extraction.

2. **Scanned/image documents** — Faxes, photographs, scanned physical documents. No embedded text. Require Optical Character Recognition (OCR) to convert pixel images into text.

The system must handle both transparently. A single document may even contain a mix (some pages digital, some scanned).

Key considerations:
- Accuracy: OCR must be high quality to feed the AI extraction stage
- Speed: OCR should not be the bottleneck (AI calls are already 5-15 seconds)
- Cost: Prefer open-source/local solutions over paid cloud APIs
- Dependencies: Minimize external service dependencies for local development
- .NET compatibility: Must integrate with .NET 10

## Decision
Implement a **dual OCR strategy**:

1. **PdfPig** (UglyToad.PdfPig) for digital PDF text extraction
   - Pure .NET library, no external dependencies
   - Extracts text with positional/layout information
   - Fast: typically < 100ms per page

2. **Tesseract OCR** (via Tesseract .NET wrapper) for scanned/image documents
   - Industry-standard open-source OCR engine
   - Supports 100+ languages
   - Good accuracy on printed text (95%+ with pre-processing)
   - Slower: 1-3 seconds per page depending on quality

**Decision logic per page:**
```
1. Attempt PdfPig text extraction
2. If extracted text < 10 characters per page → assume scanned
3. Fall back to Tesseract:
   a. Convert PDF page to high-res image (300 DPI)
   b. Pre-process: deskew, contrast enhancement (ImageSharp)
   c. Run Tesseract OCR
4. Combine text from all pages with page markers
```

## Consequences

### Positive
- Best-of-both-worlds: fast extraction for digital docs, capable OCR for scanned docs
- No cloud API costs for OCR (PdfPig and Tesseract are both free/open-source)
- Works fully offline / locally in Docker
- PdfPig preserves layout/positional info that can improve AI extraction accuracy
- Tesseract is battle-tested with decades of development

### Negative
- Tesseract requires native binaries (platform-specific). Docker handles this well, but local dev setup on Windows needs Tesseract installed or bundled.
- Tesseract accuracy degrades on handwritten text, complex layouts, or very low-quality scans. May need Azure AI Document Intelligence as a premium fallback for difficult documents.
- Image pre-processing adds complexity and potential failure points.

### Neutral
- The 10-character threshold for scanned detection may need tuning per document type
- Tesseract language packs must be included in the Docker image (English by default; add others as needed)
- Future option: add Azure AI Document Intelligence as a third "premium OCR" tier for documents where Tesseract struggles

## Alternatives Considered

**Azure AI Document Intelligence (formerly Form Recognizer)**
- Rejected as primary: Higher cost, requires Azure subscription, adds network latency. DocuSense article notes it was "less flexible and had lower performance compared to performing a local OCR." Reserved as a future premium tier.

**Google Cloud Vision OCR**
- Rejected: Cloud-only, adds vendor dependency and cost. Similar accuracy to Tesseract for printed text.

**LLM-based OCR (send image directly to GPT-4o vision)**
- Rejected as primary: Too slow (5-15 seconds per page just for text extraction) and expensive (image tokens). DocuSense article confirms: "significantly outperformed trying to use an LLM for raw text extraction, which was too slow." May be useful as a fallback for specific difficult pages.

**PaddleOCR**
- Considered: Good accuracy, especially for Asian languages. Python-based, harder to integrate with .NET. Could be added later via a sidecar container if multi-language OCR demand grows.

## References
- UglyToad.PdfPig: https://github.com/UglyToad/PdfPig
- Tesseract OCR: https://github.com/tesseract-ocr/tesseract
- Tesseract .NET wrapper: https://github.com/charlesw/tesseract
- DocuSense article: dual OCR approach (PdfPig + Tesseract) proven fastest and most reliable

## Implementation Notes (October 2026)

- The NuGet package is the official **`PdfPig`** (namespaces `UglyToad.PdfPig.*`).
- Scanned PDF pages are **not rendered at 300 DPI**. Instead, the largest embedded image on each
  scanned page is extracted with PdfPig and passed to Tesseract, which avoids a PDF rendering
  dependency. Pages drawn as vector graphics, or split across several images, aren't covered.
- Mixed PDFs are handled per page: the text layer where present, Tesseract for scanned pages.
- Image pre-processing uses **Magick.NET** (see ADR-0007), not ImageSharp. It covers EXIF
  auto-orientation, grayscale and contrast enhancement, and applies to uploaded image files.
  Deskewing is not implemented.
