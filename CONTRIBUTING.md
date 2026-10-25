# Contributing

Thanks for your interest in improving DocProc. This guide covers what you need to make a change and
get it merged. Coding conventions and repo-specific gotchas are in [CLAUDE.md](CLAUDE.md), which
applies to human contributors as much as to AI assistants.

## Prerequisites

- .NET SDK 10 (the version band is pinned in `global.json`)
- Node.js 24 LTS (22.12+ also works)
- Docker, if you want to run the full app with Aspire (PostgreSQL + Azurite); tests don't need it

## Getting set up

```bash
git clone https://github.com/c-dobkins/doc-proc.git
cd doc-proc
dotnet tool restore                      # dotnet-ef
cd src/DocumentProcessing.Web && npm ci && cd ../..
aspire run                               # optional: full app with dashboard
```

## Making a change

1. Branch from `main`.
2. Keep each commit to one logical change and use [Conventional Commits](https://www.conventionalcommits.org/)
   (`feat:`, `fix:`, `refactor:`, `test:`, `build:`, `ci:`, `docs:`, `chore:`).
3. Add or update tests. Every bug fix needs a regression test.
4. Changing the data model? Add an EF Core migration (see CLAUDE.md for the command). Never edit an
   already-applied migration.
5. Changing the API? Update `src/DocumentProcessing.Web/src/api/types.ts` and
   `src/DocumentProcessing.Api/DocumentProcessing.Api.http` too.
6. Add a line to the `[Unreleased]` section of [CHANGELOG.md](CHANGELOG.md) for user-visible changes.
7. Keep the docs true. If behaviour changes, update [SYSTEM_DESIGN.md](docs/architecture/SYSTEM_DESIGN.md)
   (including its known-limitations list). For a significant design decision, add an
   [ADR](docs/architecture/adr/README.md).

## Before opening a pull request

Run the same checks as CI:

```bash
dotnet build DocumentProcessing.slnx -c Release
dotnet test -c Release
cd src/DocumentProcessing.Web && npm run lint && npm test && npm run build
```

The PostgreSQL and Azure Storage integration tests are skipped unless you point them at a server:

```bash
DOCPROC_TEST_POSTGRES="Host=localhost;Username=postgres;Password=postgres" \
DOCPROC_TEST_AZURITE="UseDevelopmentStorage=true" \
dotnet test
```

(Start Azurite with `npx azurite-blob --inMemoryPersistence --skipApiVersionCheck`.) CI always runs
these tests.

## Reporting security issues

Please don't open public issues for vulnerabilities. See [SECURITY.md](SECURITY.md).
