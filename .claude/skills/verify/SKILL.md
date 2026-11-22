---
name: verify
description: Run the same checks as CI (Release build, backend tests, vulnerability audit, web lint/test/build) and report the results. Use before calling a change done, before committing, or when asked to check that everything still works.
---

# Verify

Run the checks in this order from the repository root and stop at the first failure. Report the
failing output, not just "it failed". A change that only passes in Debug is not done.

1. **Release build** (warnings are reported but don't fail the run; ASPIRE010 is expected):
   ```bash
   dotnet build DocumentProcessing.slnx -c Release
   ```
2. **Backend tests** (xUnit v3 on Microsoft.Testing.Platform):
   ```bash
   dotnet test --no-build -c Release
   ```
   The PostgreSQL and Azurite tests are skipped unless `DOCPROC_TEST_POSTGRES` and
   `DOCPROC_TEST_AZURITE` are set. If PostgreSQL or Azurite is reachable locally, set them, because
   database and storage changes must pass those tests:
   ```bash
   DOCPROC_TEST_POSTGRES="Host=localhost;Username=postgres;Password=postgres" \
   DOCPROC_TEST_AZURITE="UseDevelopmentStorage=true" \
   dotnet test --no-build -c Release
   ```
   (Azurite: `npx azurite-blob --inMemoryPersistence --skipApiVersionCheck`.)
3. **Vulnerable packages.** This must print no `>` lines:
   ```bash
   dotnet list DocumentProcessing.slnx package --vulnerable --include-transitive
   ```
4. **Web app** (skip if nothing under `src/DocumentProcessing.Web` changed):
   ```bash
   cd src/DocumentProcessing.Web && npm ci && npm run lint && npm test && npm run build && npm audit --audit-level=high
   ```

Finish with a short summary: each step, pass/fail, the test counts (including skipped), and
anything that needs attention.
