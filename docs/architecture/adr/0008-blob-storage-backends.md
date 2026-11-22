# ADR-0008: Azure Blob Storage in production, picked automatically

## Status
Accepted (October 2026)

## Context
Uploads were saved to the container's disk (`/data/uploads`). Azure Container Apps throws that disk
away when a replica restarts or moves, so uploads disappeared. Files need to survive restarts, with
no storage keys in configuration, and local runs and tests shouldn't need an Azure subscription.

## Decision
Add `AzureBlobStorage` next to `LocalBlobStorage`. Both implement `IBlobStorage` and follow the
same rules: paths are `yyyy/MM/dd/{guid}_{name}` with `/`, traversal-like paths are rejected, and
missing files throw `FileNotFoundException`.

`BlobStorageSelection` picks one (`BlobStorage:Provider`, default `Auto`):

| Setting found | Backend | Sign-in |
|---------------|---------|---------|
| `BlobStorage:Azure:ConnectionString`, or `ConnectionStrings:blobs` from Aspire's Azurite | Azure Blob | Connection string |
| `BlobStorage:Azure:ServiceUri`, or `AZURE_STORAGE_BLOB_ENDPOINT` from azd | Azure Blob | Managed identity, no keys |
| Neither | Local disk | None. A warning is logged outside Development |

`azure.yaml` adds a storage account with a `documents` container, and azd gives the app's managed
identity *Storage Blob Data Contributor*. Locally, Aspire runs the Azurite emulator, so local runs
use the same code as production.

## Consequences
- Uploads survive restarts, and storage needs no secrets.
- A new environment needs no storage settings: azd and Aspire provide them.
- One contract test suite runs against both backends, with Azurite in CI.
- A deployment that skips provisioning falls back to local disk, and only a startup warning says
  so. The storage account also adds a small cost.

## Alternatives considered
- **Azure Files mounted as a volume.** Keeps the file code, but adds SMB mount setup and is slower
  and costlier for this use.
- **Files in PostgreSQL.** Bloats the database and backups, and suits 50 MB files poorly.
- **An explicit setting only.** Easy to forget. `Provider` can still be forced to `Local` or `Azure`.
