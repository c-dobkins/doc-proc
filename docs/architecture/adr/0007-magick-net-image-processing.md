# ADR-0007: Magick.NET for image clean-up

## Status
Accepted (October 2026). Replaces SixLabors.ImageSharp.

## Context
Uploaded images are checked and cleaned up before OCR (EXIF orientation, grayscale, contrast). This
used ImageSharp 3.1.12, which has a published security advisory. Upgrading wasn't an option: from
3.2 on, ImageSharp runs a license check that fails every non-Debug build without a key, which breaks
the Release build in the Dockerfile.

The replacement had to read JPEG, PNG, TIFF and BMP, run on Linux in the container, need no
license key, and be actively maintained.

## Decision
Use Magick.NET (`Magick.NET-Q8-AnyCPU`, Apache-2.0). It ships native ImageMagick builds for Linux,
macOS and Windows.

ImageMagick understands many formats, some of them script-like (MVG, MSL, SVG). So images are
always decoded with the coder for the file's declared, signature-checked type
(`ImageDecoding.ReadSettingsFor`), never by sniffing the content. Global limits cap pixel size and
memory.

The clean-up also moved into `DocumentPreProcessor`, so the Tesseract engine only does OCR.

## Consequences
- No license key, Release and Docker builds work, and no known vulnerabilities.
- TIFF works, which matters for multi-page scans and faxes.
- Fixing the decoder per type closes off format-confusion attacks.
- The native payload is bigger than a fully managed library, and ImageMagick has a history of
  security bugs, so it needs keeping up to date (Dependabot does this).

## Alternatives considered
- **Buy an ImageSharp license.** Adds secret handling to CI and the Docker build for a portfolio
  project.
- **SkiaSharp.** Can't read TIFF.
- **Stay on ImageSharp 3.1.x.** Keeps a known vulnerability with no way forward.
