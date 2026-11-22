# ADR-0006: Public reads, API key for writes

## Status
Accepted (October 2026)

## Context
The app runs publicly as a demo. At first it had no auth at all, so anyone could upload files
(spending AI credit), edit or approve documents, and delete templates. Visitors should still be
able to look around without signing up, and a deployment that forgets to set up access mustn't be
left open.

## Decision
- **Reads are anonymous.** Anyone can list and view documents, templates and metrics, and download
  files.
- **Writes need an API key.** Every endpoint that changes data or triggers processing requires the
  `write` policy, met by an `X-Api-Key` header that matches one of `Auth:ApiKeys`. Keys come from
  secrets or environment variables, must be at least 24 characters, and are compared in constant
  time over SHA-256 digests.
- **Fail closed.** With `Auth:RequireApiKeyForWrites` on (the default) and no keys set, the app is
  read-only and logs a warning. Development turns enforcement off.
- The UI asks `GET /api/auth/status` whether a key is needed, shows a read-only banner, and lets
  you enter a key. The key is kept in `localStorage` and sent with each request.
- Auth runs before the rate limiter, so rejected calls don't use up processing permits.

## Consequences
- The demo stays open to browse, and only key holders can upload or spend AI credit.
- No identity provider to run, and no setup locally.
- A misconfigured deployment ends up read-only rather than open.
- There are no user identities: every key holder can do everything, and the correction log can't
  name a person.
- Uploaded documents can be read by anyone, so a public deployment isn't for sensitive files.
- Keys are shared secrets and are rotated by hand: add the new key, deploy, remove the old one.

## Alternatives considered
- **Entra ID or Container Apps built-in auth.** It would lock anonymous visitors out. It's the
  next step if real accounts are needed.
- **ASP.NET Core Identity.** User management and password resets are too much for a demo.
- **No writes at all.** I need to be able to upload and show the pipeline working.
