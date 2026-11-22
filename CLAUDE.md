# CLAUDE.md

DocProc: a .NET 10 + React document-processing platform. It runs uploaded PDFs/images through
OCR (PdfPig or Tesseract), then AI field extraction (Azure OpenAI/OpenAI/Gemini/Ollama), then
human review. The Aspire AppHost orchestrates local dev; production is one Docker image on Azure
Container Apps.

## Commands

Run from the repo root unless noted.

| Task | Command |
|---|---|
| Build everything | `dotnet build DocumentProcessing.slnx` |
| Backend tests (unit + API integration) | `dotnet test` (xUnit v3 on Microsoft.Testing.Platform, see `global.json`) |
| One test class / method | `dotnet test --filter-class "*OcrEngineRouterTests"` / `--filter-method "*Upload*"` |
| Coverage + TRX | `dotnet test --coverage --coverage-output-format cobertura --coverage-settings "$PWD/tests/DocumentProcessing.Tests/coverage.config.xml" --report-trx` (settings path must be absolute; the file must contain no XML comments) |
| Include PostgreSQL / Azurite tests | `DOCPROC_TEST_POSTGRES="Host=localhost;Username=postgres;Password=postgres" DOCPROC_TEST_AZURITE="UseDevelopmentStorage=true" dotnet test` |
| Add an EF migration | `dotnet tool restore`, then `dotnet ef migrations add <Name> -p src/DocumentProcessing.Infrastructure -s src/DocumentProcessing.Infrastructure -o Data/Migrations` |
| Vulnerable packages | `dotnet list DocumentProcessing.slnx package --vulnerable --include-transitive` |
| Format / unused usings | `dotnet format DocumentProcessing.slnx` |
| Run the full app (needs Docker) | `aspire run` |
| Web: install / lint / test / build | `cd src/DocumentProcessing.Web && npm ci && npm run lint && npm test && npm run build` |

Before calling work done, run the `verify` skill (`.claude/skills/verify`), or at least
`dotnet test` plus `npm run lint && npm test && npm run build` if you touched the web app. CI
(`.github/workflows/ci.yml`) runs the same checks, plus a Release build and `docker build`. A
change that only passes in Debug is not done.

## Layout

```
DocumentProcessing.AppHost/        Aspire orchestrator: PostgreSQL + Azurite + api + Vite web app
DocumentProcessing.ServiceDefaults/ OpenTelemetry, health checks (/health, /alive in Dev), HTTP resilience
src/DocumentProcessing.Core/       Domain models, enums, interfaces. No infrastructure dependencies.
src/DocumentProcessing.Infrastructure/
  Ai/        AiProviderBase (shared flow) + one class per provider; PromptBuilder; ExtractionResponseParser
  Ocr/       OcrEngineRouter (digital vs scanned vs mixed), PdfPig + Tesseract engines, DocumentPreProcessor
  Pipeline/  IngestionHandler (stage 1: store blob + create Document), DocumentPipeline (pre-process,
             OCR, AI, post-process, confidence routing)
  Data/      EF Core DbContext, IEntityTypeConfiguration classes, repositories, SeedData
  Storage/   AzureBlobStorage + LocalBlobStorage (BlobStorageSelection picks one), StoragePaths
  DependencyInjection.cs  AddInfrastructure(): the only place infra services are registered
src/DocumentProcessing.Api/        Minimal API endpoint groups, SignalR hub, validators, middleware, Program.cs
src/DocumentProcessing.Worker/     Class library: DocumentProcessingWorker (BackgroundService), hosted in-process by the Api
src/DocumentProcessing.Web/        React 19 + Vite 8 + Tailwind 4 + TanStack Query + React Router 8 SPA
tests/DocumentProcessing.Tests/    xUnit + NSubstitute; Api/ holds WebApplicationFactory integration tests
docs/architecture/                 SYSTEM_DESIGN.md + ADRs (read the relevant ADR before changing architecture)
```

Request flow: `POST /api/documents/upload` validates the file, calls `IIngestionHandler` (which
saves the blob and the `Document` row), then writes the id to an in-memory `Channel<Guid>`. New
ingestion sources should reuse `IIngestionHandler`. `DocumentProcessingWorker` reads the id, runs
`IDocumentPipeline.ProcessAsync`, and pushes status over SignalR (`/hubs/processing`).

## Conventions

- **C#:** file-scoped namespaces, primary constructors, `_camelCase` private fields, nullable
  enabled. Shared MSBuild settings live in `Directory.Build.props`; don't re-add
  `TargetFramework`/`Nullable`/`ImplicitUsings` to csproj files.
- **Options pattern:** every option class has a `SectionName` and is bound in `AddInfrastructure`
  or `Program.cs`. Add new settings to `appsettings.json` with safe defaults. Secrets (API keys,
  real connection strings) go in user secrets or environment variables, never in committed files.
- **New AI provider:** subclass `AiProviderBase`, implement `CallApiAsync`/`IsAvailableAsync`,
  add an options class to `AiProviderOptions`, and register it under its `Enabled` flag in
  `DependencyInjection.cs`. Send API keys in headers, never in URLs, because URLs get logged and traced.
- **Endpoints:** add them to the matching `*Endpoints.cs` group. Validate request bodies with a
  FluentValidation validator plus `ValidationFilter<T>`. Anything that changes data must call
  `.RequireAuthorization(AuthPolicies.Write)`. Anything that triggers OCR/AI must also call
  `.RequireRateLimiting(DocumentEndpoints.ProcessingRateLimitPolicy)`. Reads stay anonymous.
- **Errors:** let unexpected exceptions reach `GlobalExceptionHandler`, which returns ProblemDetails.
  Providers wrap failures in `AiExtractionException`. Always rethrow `OperationCanceledException`.
- **Frontend:** import routing from `react-router` (not `react-router-dom`). The PDF viewer uses
  pdf.js's `legacy` build for browser compatibility; keep it. Data access goes through the hooks
  in `src/api/hooks.ts`; don't call `fetch` from components. Lint must pass with zero warnings.
- **Contracts:** `src/api/types.ts` mirrors `Api/DTOs/DTOs.cs`, including the SignalR
  `StatusUpdated` payload (`StatusUpdatedMessage`, sent as a single argument). Change both
  sides together, and update `DocumentProcessing.Api.http` when endpoints change.
- **Tests:** name them `Method_Scenario_Expectation`. Mock interfaces with NSubstitute. Use SQLite
  in-memory for repository and EF tests. Every bug fix gets a regression test. Where they go:
  `tests/DocumentProcessing.Tests/Api/` (HTTP via `WebApplicationFactory`), `Database/` (migrations,
  PostgreSQL), `Storage/` (blob contract), otherwise next to the feature; web tests sit beside the
  component (`*.test.tsx`).
- **Commits:** Conventional Commits (`feat:`, `fix:`, `test:`, `build:`, `ci:`, `docs:`,
  `chore:`), each one a single logical change.

## Gotchas

- **API host tests must run serially.** Concurrent `WebApplicationFactory` hosts break the
  static entry point, so put every test class that creates one in
  `[Collection(ApiHostCollection.Name)]`.
- **SQLite test shims** in `tests/.../Api/ApiFactory.cs`: client-side ids for `TemplateField`'s
  composite owned key, and a `DateTimeOffset` converter. Production uses Npgsql. Check
  provider-specific behaviour against PostgreSQL semantics.
- **Schema changes need a migration.** `DatabaseInitializer` (called from `Program.cs`) applies EF
  migrations on PostgreSQL. It baselines databases created before migrations existed, but only if
  their schema matches. `MigrationModelTests` fails when the model changes without a migration.
  SQLite (tests) still uses `EnsureCreated`, so write migrations against PostgreSQL semantics.
  Never edit an already-applied migration; add a new one.
- **Images are decoded with Magick.NET using an explicit format** (`Ocr/ImageDecoding.cs`), never
  by content sniffing, under global resource limits. Keep it that way; ImageMagick's
  auto-detected coders (MVG, MSL, SVG) are a known attack surface. Don't reintroduce
  SixLabors.ImageSharp: 3.2+ fails Release builds without a license key.
- **Two storage backends, one contract.** `BlobStorageSelection` uses Azure Blob when a connection
  string (`ConnectionStrings:blobs`, set by Aspire's Azurite) or endpoint (`AZURE_STORAGE_BLOB_ENDPOINT`,
  set by azd) exists, else local disk. Paths are always `yyyy/MM/dd/{guid}_{name}` with `/`; both
  reject traversal-like paths and throw `FileNotFoundException` for missing files. Any storage change
  must pass `BlobStorageContractTests` for both (`DOCPROC_TEST_AZURITE=UseDevelopmentStorage=true`
  with Azurite running).
- **`/health` is mapped by `MapDefaultEndpoints` in Development** and by `Program.cs` elsewhere.
  Mapping it twice makes the route ambiguous and it returns 500.
- **The Worker is a library, not a host.** The Api is the only executable (besides AppHost).
  Register background services in `Api/Program.cs`.
- **Auth is API-key based** (`Api/Auth/ApiKeyAuthentication.cs`). Reads are public; writes need
  `X-Api-Key`. Development disables enforcement, so integration tests that check auth use
  `KeyProtectedApiFactory`. Outside Development, no configured keys means read-only (fail closed).
- **Tests run on Microsoft.Testing.Platform**, not VSTest: `--filter`, `--logger` and `--collect`
  don't exist; use the MTP options above. Pass `TestContext.Current.CancellationToken` to async
  APIs in tests (analyzer xUnit1051).
- Tests don't need Tesseract native libraries, Postgres or Docker. The Tesseract engine is
  mocked behind `ITesseractEngine`.

## Held-back versions

Don't "fix" these when updating packages:
- **TypeScript stays on 6.0.x:** `typescript-eslint` doesn't support TypeScript 7 (the native compiler).
- **MessagePack stays on 2.5.x** (pinned in the AppHost): Aspire 13.6 depends on 2.x.
- **.NET stays on 10** (LTS) until .NET 11 is released.

## Docs

- `docs/architecture/SYSTEM_DESIGN.md` describes the system as built, including known limitations.
  Update it when behaviour changes.
- `docs/architecture/adr/`: add an ADR for significant decisions. Don't rewrite old ADRs; append a
  dated "Implementation Notes" section instead.
- `CHANGELOG.md`: add user-visible changes under `[Unreleased]`.
