# ADR-0006: Public Reads, API Key for Writes

## Status
Accepted (October 2026)

## Context
The application is deployed publicly as a portfolio demo. Originally it had no authentication at
all: anyone could upload files (which uses paid AI quota), edit or approve documents, and delete
templates.

Requirements:
- Anonymous visitors must be able to explore the demo without signing up.
- Only trusted people should be able to change data or trigger OCR/AI processing.
- It must be simple to operate: no identity provider to run, and nothing extra to configure locally.
- It must be safe by default: a deployment that forgets to configure access must not be open.

## Decision
- **Reads are anonymous.** Listing and viewing documents, templates and metrics, and downloading
  files, require no credentials.
- **Writes require an API key.** Every endpoint that changes data or triggers processing requires
  the `write` authorization policy, which an `X-Api-Key` header matching one of `Auth:ApiKeys`
  satisfies.
  - Keys come from secrets or environment variables and must be at least 24 characters.
  - They are compared in constant time over SHA-256 digests.
  - A 401 response is returned as ProblemDetails.
- **Fail closed.** With `Auth:RequireApiKeyForWrites` on (the default) and no keys configured, the
  app is read-only and logs a warning. Development turns enforcement off.
- The UI asks `GET /api/auth/status` whether writes need a key, shows a read-only banner, and lets
  the user enter a key. The key is kept in `localStorage` and sent on every request.
- Authorization runs before the rate limiter, so rejected calls don't consume processing permits.

## Consequences

### Positive
- The demo stays explorable, while uploads and AI spending are limited to key holders.
- No external identity provider is needed, and local development has no setup.
- A misconfigured deployment ends up read-only rather than open.

### Negative
- There are no user identities: every key holder has full write access, and audit trails can't
  name a person.
- Uploaded documents are readable by anyone, so this is unsuitable for sensitive data.
- Keys are shared secrets and must be rotated manually (add the new key, deploy, remove the old one).

## Alternatives Considered

**Microsoft Entra ID / Container Apps "Easy Auth"**
- Rejected for now: it would lock anonymous visitors out of the demo entirely, and it lives in
  infrastructure configuration. It's the natural next step if real user accounts are needed.

**ASP.NET Core Identity (local accounts)**
- Rejected: user management, password reset and storage are out of proportion for a demo.

**Read-only demo only (no writes at all)**
- Rejected: the owner needs to be able to upload and show the pipeline working.
