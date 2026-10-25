# ADR-0002: Use PostgreSQL as Primary Database

## Status
Accepted, with implementation notes (October 2026)

## Context
The platform needs to persist documents, processing results, extraction templates, processing metrics, and correction logs. The data model is primarily relational: documents have processing results, results contain extracted fields, templates define field schemas.

Key requirements:
- ACID transactions for document status updates (prevent double-processing)
- Structured storage for extraction templates and field definitions
- Flexible storage for extracted field values (variable schema per document type)
- Full-text search over document metadata (future)
- Must work well in Docker (local dev) and Azure (production)
- Team has .NET / EF Core experience

## Decision
Use **PostgreSQL 16** as the primary database, accessed via **Entity Framework Core 10** with the Npgsql provider.

Use PostgreSQL's **JSONB** column type for flexible data like extracted fields and template field definitions, combining relational integrity with schema flexibility.

## Consequences

### Positive
- ACID compliance ensures document status consistency under concurrent processing
- JSONB provides flexible field storage without sacrificing query capability — can index and query extracted field values
- Excellent EF Core support via Npgsql provider (migrations, LINQ, JSONB mapping)
- First-class Docker support (official PostgreSQL image)
- Azure Database for PostgreSQL available as managed service for production
- Free and open source, no licensing costs
- Rich feature set: CTEs, window functions, full-text search, partial indexes

### Negative
- Vertical scaling has limits (mitigated by read replicas for read-heavy API)
- Requires connection pooling at scale (PgBouncer or Npgsql built-in pooling)
- Team may need to learn PostgreSQL-specific features (JSONB operators, indexing strategies)

### Neutral
- EF Core abstracts most PostgreSQL-specific concerns, making future migration to SQL Server feasible if needed
- Need to decide on JSONB vs. separate normalized tables for extracted fields (decision: JSONB for MVP, normalize if query patterns demand it)

## Alternatives Considered

**SQL Server**
- Rejected for MVP: Heavier Docker image, licensing costs for production. EF Core provider swap is straightforward if needed later.

**MongoDB**
- Rejected: While the flexible schema is appealing for extracted fields, the document status lifecycle (Uploaded → Processing → Completed/NeedsReview → Approved) benefits from ACID transactions. Cross-collection transactions in MongoDB add complexity. Team has stronger RDBMS experience.

**SQLite**
- Rejected: Insufficient for concurrent write access from multiple Worker threads. No production-grade managed hosting.

**Azure Cosmos DB**
- Rejected: Higher cost, eventual consistency by default. The data access patterns (primarily single-document reads/writes with relational queries for listings) don't justify a globally distributed database.

## References
- Database Selection reference guide
- PostgreSQL JSONB documentation: https://www.postgresql.org/docs/current/datatype-json.html
- Npgsql EF Core provider: https://www.npgsql.org/efcore/

## Implementation Notes (October 2026)

- **JSONB was not used.** Extracted fields live in a normalized `DocumentFields` table, and template
  fields in an owned `TemplateField` table. The "normalize if query patterns demand it" option was
  taken from the start. JSONB remains an option for genuinely schemaless data.
- The schema is managed by **EF Core migrations**, applied at startup by `DatabaseInitializer`.
  Databases created earlier by `EnsureCreated` are baselined after their schema is verified.
- The server version isn't pinned by the app: CI tests against PostgreSQL 18, and production uses
  whatever version azd provisions for Azure Database for PostgreSQL Flexible Server.
