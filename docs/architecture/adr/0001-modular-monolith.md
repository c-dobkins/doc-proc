# ADR-0001: Use Modular Monolith Architecture

## Status
Accepted, with implementation notes (October 2026)

## Context
We are building an AI-powered document processing platform with a 7-stage pipeline (ingestion → pre-processing → OCR → AI extraction → post-processing → storage → UI review). The system needs to be developed by a small team and deployed initially via Docker Compose locally, with Azure Cloud as the production target.

We need to decide the overall architectural pattern: monolith, modular monolith, microservices, or serverless.

**Forces at play:**
- Small team (1-3 developers initially)
- Need for rapid iteration on pipeline stages
- Pipeline stages are tightly coupled in execution order
- Future need to scale processing independently from API serving
- No immediate need for polyglot technology
- Development speed is more important than infinite scalability at this stage

## Decision
Use a **modular monolith** architecture with clearly separated projects (Api, Core, Infrastructure, Worker, Web) sharing a single solution. The Worker runs as a separate process but shares code with the API via the Core and Infrastructure libraries.

The processing pipeline uses an in-memory `Channel<T>` to decouple the API (which accepts uploads) from the Worker (which processes documents), enabling async processing without the operational complexity of a message broker.

## Consequences

### Positive
- Simple deployment: two .NET processes (API + Worker) + React SPA
- No distributed system complexity (no service discovery, distributed tracing, saga patterns)
- Shared domain models and interfaces eliminate duplication
- Easy local development and debugging
- Clean module boundaries allow future extraction to microservices if needed
- Fast inter-module communication (in-process method calls)

### Negative
- API and Worker must be deployed together (shared libraries)
- Cannot scale API and Worker independently with different tech stacks
- In-memory Channel loses messages on process crash (acceptable for MVP; mitigated by stale-document recovery on startup)

### Neutral
- Team must maintain discipline on module boundaries (no circular dependencies between Api ↔ Infrastructure)
- Worker can be split into a separate deployable later by replacing Channel with a message broker

## Alternatives Considered

**Full Microservices**
- Rejected: Excessive operational complexity for a small team. Each pipeline stage as a service would require inter-service communication, distributed tracing, and separate deployments. Only warranted with 20+ developers.

**Pure Monolith (single process)**
- Rejected: Processing documents synchronously in the API request thread would block HTTP connections and degrade API responsiveness. Background processing requires at least a logical separation.

**Serverless (Azure Functions)**
- Rejected: Cold starts would impact processing latency. Function chaining for the pipeline adds orchestration complexity (Durable Functions). Vendor lock-in. Local development experience is inferior to Docker.

## References
- Architecture Patterns reference guide
- DocuSense article: pipeline-based architecture for document processing

## Implementation Notes (October 2026)

- The Worker does **not** run as a separate process. `DocumentProcessing.Worker` is a class library,
  and `DocumentProcessingWorker` is hosted inside the API process, so the deployable unit is a single
  API process plus the static SPA, shipped as one Docker image.
- Local development uses .NET Aspire (PostgreSQL, Azurite, API, Vite) rather than Docker Compose.
- Splitting the worker out remains possible as described above (replace the channel with a broker).
