# ADR-0007: Magick.NET for Image Pre-processing

## Status
Accepted (October 2026), superseding the use of SixLabors.ImageSharp

## Context
Uploaded images are validated and normalized before OCR: EXIF auto-orientation, grayscale and
contrast. This was done with SixLabors.ImageSharp 3.1.12, which:
- has a published security advisory
- can't simply be upgraded: from 3.2 onwards (including 4.x), the package runs a license check
  that fails every non-Debug build without a Six Labors license key, which breaks the Release
  build in the Dockerfile

Requirements: decode JPEG, PNG, **TIFF** and BMP; run on Linux in the container image; no license
key or paid tier; actively maintained.

## Decision
Use **Magick.NET** (`Magick.NET-Q8-AnyCPU`, Apache-2.0), which bundles native ImageMagick builds for
Linux, macOS and Windows and needs only glibc.

Because ImageMagick supports many formats, some of them script-like (MVG, MSL, SVG), images are
always decoded with the coder matching the file's **declared, signature-checked type**
(`ImageDecoding.ReadSettingsFor`), never by content sniffing. Global resource limits cap pixel
dimensions and memory.

Normalization was also moved into `DocumentPreProcessor`, the pipeline stage meant for it, so the
Tesseract engine only performs OCR.

## Consequences

### Positive
- No license key; Release and Docker builds work; no known vulnerabilities.
- TIFF support (multi-page faxes and scans are common inputs).
- An explicit decoder per type removes a class of format-confusion attacks.

### Negative
- Larger native payload than a fully managed library.
- ImageMagick has historically had a large attack surface. This is mitigated by the explicit coder
  and resource limits, plus the upload signature check, but it still needs keeping up to date
  (Dependabot).

## Alternatives Considered

**Buy or obtain a Six Labors license key**
- Rejected: it adds secret management to CI and the Docker build for a portfolio project, and
  depends on licensing terms continuing to fit.

**SkiaSharp (MIT)**
- Rejected: it doesn't decode TIFF.

**Stay on ImageSharp 3.1.x**
- Rejected: it keeps a known vulnerability, with no upgrade path that avoids the license check.
