# ADR-0005: PdfPig for digital PDFs, Tesseract for scans

## Status
Accepted (October 2026)

## Context
Documents arrive as digital PDFs with a text layer, as scans or photos with no text, or as PDFs
that mix both. OCR has to be good enough to feed the AI step, quick, free to run, and work offline
in a container.

## Decision
Use two engines, routed by `OcrEngineRouter`:

- **Images** go to Tesseract.
- **PDFs** are read with PdfPig first. A page with fewer than 10 characters of text counts as
  scanned, and its largest embedded image is extracted and run through Tesseract.
- **Mixed PDFs** are handled page by page.
- If PdfPig can't open a PDF, the whole file goes through Tesseract.

Images are cleaned up before OCR with Magick.NET: EXIF orientation, grayscale and contrast
([ADR-0007](0007-magick-net-image-processing.md)).

## Consequences
- Digital PDFs are fast and exact, and scans still work, with no per-page cloud cost.
- Reading the embedded image avoids a PDF rendering dependency. Pages drawn as vector graphics, or
  split across several images, aren't covered.
- Tesseract needs native libraries and language data. The Docker image includes them, and tests
  don't need them.
- Tesseract struggles with handwriting and poor scans. Deskewing isn't done.

## Alternatives considered
- **Azure AI Document Intelligence.** Costs more, needs a subscription, and adds a network hop. It
  could be added later for hard documents.
- **Sending page images to a vision model.** Slow and expensive just to get the text.
- **Google Cloud Vision.** Another cloud dependency for similar results on printed text.
- **PaddleOCR.** Python, so harder to host next to .NET.

## Background reading
- [Advancing document processing using AI](https://newsletter.techworld-with-milan.com/p/advancing-document-processing-using-ai)
  (Tech World with Milan), the DocuSense write-up that shaped the pipeline, the dual OCR approach
  and calling the AI SDKs directly.
