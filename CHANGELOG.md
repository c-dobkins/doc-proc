# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-10-25

First public release.

### Features
- Upload PDF, JPEG, PNG, TIFF and BMP documents (up to 50 MB) through the API or the React UI.
- Dual OCR: PdfPig for digital PDFs and Tesseract for scans and images, including mixed PDFs
  page by page. Images are normalized first (orientation, grayscale, contrast) with Magick.NET.
- AI field extraction with configurable templates. Supports Azure OpenAI (through the OpenAI SDK
  and Azure's v1 endpoint), OpenAI, Google Gemini and Ollama, with a configurable fallback order.
- Post-processing: type-specific normalization and validation, required-field checks, and
  per-field and per-document confidence scores. Documents below the auto-approve threshold go to
  human review.
- Review UI: PDF viewer beside editable fields, correction logging, and an approval step.
  Processing status is pushed live over SignalR.
- Templates editable at runtime (versioned), a dashboard and processing metrics.

### Platform
- ASP.NET Core 10 Minimal APIs with an in-process background worker (`Channel<T>`); crash recovery
  and graceful shutdown.
- PostgreSQL via EF Core migrations, applied at startup. Databases created before migrations are
  baselined safely.
- File storage in Azure Blob Storage (managed identity) or on local disk, selected automatically.
  The Azurite emulator is used locally.
- .NET Aspire for local development; one Docker image deployed to Azure Container Apps with `azd`.
- OpenTelemetry tracing and metrics, and structured logging with Serilog.

### Security
- Public read-only access; uploads, edits and processing require an access key (`X-Api-Key`).
  The app fails closed when no key is configured.
- Rate limiting on endpoints that trigger OCR/AI; upload size, extension and file-signature
  checks; images decoded only by the coder matching their type; path-traversal protection in
  both storage backends.

### Quality
- Over 240 backend tests (xUnit v3 on Microsoft.Testing.Platform):
  - unit tests
  - API integration tests (`WebApplicationFactory`)
  - PostgreSQL migration tests
  - a storage contract suite run against Azure (Azurite) and local disk
- Frontend: ESLint (zero warnings) and Vitest + Testing Library.
- GitHub Actions CI: build, tests with coverage, lint, dependency vulnerability audit, Docker
  build. Dependabot keeps dependencies current.
- Architecture documented in a system design and eight ADRs.

[Unreleased]: https://github.com/c-dobkins/DocumentProcessing/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/c-dobkins/DocumentProcessing/releases/tag/v1.0.0
