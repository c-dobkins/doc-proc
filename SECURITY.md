# Security Policy

## Scope and current posture

DocProc is a portfolio/demo project with a deliberately simple access model: **reads are public, writes need
an access key.** Browsing documents, templates and metrics is anonymous. Every endpoint that changes data or
triggers OCR/AI processing (upload, reprocess, field edits, approve, template create/update/delete) requires
an `X-Api-Key` header that matches one of `Auth:ApiKeys`. The reasoning is in
[ADR-0006](docs/architecture/adr/0006-api-key-access-model.md).

- Keys are configured through environment variables / secrets (e.g. `Auth__ApiKeys__0`), must be at least
  24 characters, and are compared in constant time.
- The app **fails closed**: in non-Development environments, if no key is configured, it runs read-only.
- Development (`appsettings.Development.json`) disables enforcement for local work.

Built-in safeguards:

- Rate limiting on endpoints that trigger OCR/AI processing (`RateLimiting:ProcessingPermitsPerMinute`)
- Upload size limit (50 MB) and file-signature (magic byte) validation
- Path-traversal protection in both storage backends; production files are in Azure Blob Storage,
  accessed with a managed identity (no storage keys)
- Images are decoded only by the coder matching their declared type (no content sniffing), with
  pixel and memory limits
- Secrets (AI provider keys) are read from user secrets / environment variables, never committed
- Dependency scanning: `dotnet list package --vulnerable` and `npm audit` run in CI; Dependabot keeps packages current

Known limitations:

- There are no user accounts: anyone holding a key has full write access, and every uploaded document can
  be read anonymously. Do not use a public deployment for sensitive documents.
- Keys are shared secrets. Rotate one by adding the new key, redeploying, then removing the old key.

## Reporting a vulnerability

Please **do not open a public issue**. Report it privately through GitHub's
[private vulnerability reporting](https://github.com/c-dobkins/doc-proc/security/advisories/new)
with steps to reproduce and the affected version or commit.
