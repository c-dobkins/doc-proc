# ADR-0008: Azure Blob Storage in Production, Selected Automatically

## Status
Accepted (October 2026)

## Context
Uploaded files were stored on the container's local filesystem (`/data/uploads`). Azure Container
Apps discards that disk when a replica restarts or moves, so uploads disappeared. Only the seed
documents were regenerated.

Requirements:
- Files survive restarts and redeployments.
- No storage keys in configuration.
- Local development and tests work without an Azure subscription, ideally through the same code
  path as production.
- Paths stored in the database stay valid whichever backend is used.

## Decision
Add `AzureBlobStorage` next to `LocalBlobStorage`, both implementing `IBlobStorage` with one
contract:
- the same path scheme, `yyyy/MM/dd/{guid}_{name}` with `/`
- traversal-like paths rejected
- `FileNotFoundException` for missing files

`BlobStorageSelection` picks the backend from configuration (`BlobStorage:Provider`, default `Auto`):

| Setting found | Backend | Authentication |
|---------------|---------|----------------|
| `BlobStorage:Azure:ConnectionString` or `ConnectionStrings:blobs` (set by Aspire for Azurite) | Azure Blob | Connection string (dev) |
| `BlobStorage:Azure:ServiceUri` or `AZURE_STORAGE_BLOB_ENDPOINT` (set by azd) | Azure Blob | Managed identity via `DefaultAzureCredential` (keyless) |
| none of the above | Local disk | — (a warning is logged outside Development) |

`azure.yaml` declares a `storage` resource with a `documents` container. azd grants the app's
managed identity *Storage Blob Data Contributor* and sets `AZURE_STORAGE_BLOB_ENDPOINT` and
`AZURE_CLIENT_ID`. The Aspire AppHost runs the Azurite emulator, so local runs use the Azure code
path too.

## Consequences

### Positive
- Uploads are durable in production, and no secrets are needed for storage.
- Setting up a new environment needs no configuration: azd and Aspire provide the settings.
- One contract test suite runs against both backends (Azure via Azurite in CI).

### Negative
- An extra Azure resource (small cost).
- A deployment that skips provisioning silently falls back to local disk, with only a startup
  warning to show for it.

## Alternatives Considered

**Azure Files volume mounted into the container**
- Rejected: it keeps the filesystem code, but adds SMB mount configuration and is slower and
  costlier for this access pattern.

**Store files in PostgreSQL (bytea)**
- Rejected: it bloats the database and backups, and is a poor fit for 50 MB files.

**Explicit provider setting only (no auto-detection)**
- Rejected as the default: it adds a configuration step that's easy to forget. `Provider = Local`
  or `Azure` can still be forced.
