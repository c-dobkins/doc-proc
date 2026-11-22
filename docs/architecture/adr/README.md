# Architecture decisions

Each record covers one decision: the problem, what I chose, what it costs, and what else I looked
at. Each one describes the code as it is now.

| ADR | Decision |
|-----|----------|
| [0001](0001-modular-monolith.md) | Modular monolith, with the worker inside the API |
| [0002](0002-postgresql-database.md) | PostgreSQL with EF Core migrations |
| [0003](0003-multi-provider-ai.md) | AI providers behind one interface, with a fallback order |
| [0004](0004-channel-based-async.md) | In-memory `Channel<T>` as the work queue |
| [0005](0005-dual-ocr-strategy.md) | PdfPig for digital PDFs, Tesseract for scans |
| [0006](0006-api-key-access-model.md) | Public reads, API key for writes |
| [0007](0007-magick-net-image-processing.md) | Magick.NET for image clean-up |
| [0008](0008-blob-storage-backends.md) | Azure Blob Storage in production, picked automatically |

To add one, copy an existing record (Status, Context, Decision, Consequences, Alternatives
considered), take the next number, and add it to this table.
