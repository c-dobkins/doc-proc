# Architecture Decision Records

Each ADR records a significant decision: its context, the choice, its consequences, and the
alternatives considered. ADRs aren't rewritten when the code changes. Where the implementation
diverged from the original decision, a dated **Implementation Notes** section says how.

| ADR | Decision | Status |
|-----|----------|--------|
| [0001](0001-modular-monolith.md) | Modular monolith; worker hosted in the API process | Accepted (with notes) |
| [0002](0002-postgresql-database.md) | PostgreSQL via EF Core; normalized tables, migrations | Accepted (with notes) |
| [0003](0003-multi-provider-ai.md) | AI providers behind a common interface, with a fallback order | Accepted (with notes) |
| [0004](0004-channel-based-async.md) | In-memory `Channel<T>` work queue | Accepted (with notes) |
| [0005](0005-dual-ocr-strategy.md) | PdfPig for digital PDFs, Tesseract for scans | Accepted (with notes) |
| [0006](0006-api-key-access-model.md) | Public reads, API key for writes | Accepted |
| [0007](0007-magick-net-image-processing.md) | Magick.NET for image pre-processing | Accepted |
| [0008](0008-blob-storage-backends.md) | Azure Blob Storage in production, selected automatically | Accepted |

To add one, copy the structure of an existing ADR (Status, Context, Decision, Consequences,
Alternatives Considered), use the next number, and add it to this table.
