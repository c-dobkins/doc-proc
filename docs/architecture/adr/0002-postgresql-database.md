# ADR-0002: PostgreSQL with EF Core migrations

## Status
Accepted (October 2026)

## Context
The app stores documents, processing results, extracted fields, templates and correction logs. The
data is relational: a document has a result, a result has fields, a template has field definitions.
Status changes need transactions so a document isn't processed twice. The database has to run in a
container locally and as a managed service on Azure.

## Decision
Use PostgreSQL through EF Core 10 and Npgsql, with normal tables for extracted fields
(`DocumentFields`) and template fields (an owned `TemplateField` table).

I first planned to keep extracted fields in JSONB columns. Normal tables turned out simpler to
query and validate, so JSONB wasn't used.

The schema is managed with EF Core migrations, applied at startup by `DatabaseInitializer`.
Databases created before migrations existed (with `EnsureCreated`) are baselined, but only after
checking that every table and column matches. A PostgreSQL advisory lock stops two replicas from
migrating at once.

## Consequences
- Proper transactions, foreign keys and a familiar toolset.
- Azure Database for PostgreSQL Flexible Server in production, a container locally (Aspire), and
  PostgreSQL 18 in CI.
- Tests use SQLite in memory for speed, so provider-specific behaviour has to be checked against
  PostgreSQL. CI runs the migration tests on a real server.

## Alternatives considered
- **SQL Server.** Heavier container image, and licensing costs in production.
- **MongoDB.** The status lifecycle relies on transactions, which are more awkward across
  collections.
- **Cosmos DB.** More expensive and more than this access pattern needs.
- **SQLite.** Not suited to concurrent writes from several workers, and no managed hosting.
