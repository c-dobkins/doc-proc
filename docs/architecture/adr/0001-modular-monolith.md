# ADR-0001: Modular monolith, with the worker inside the API

## Status
Accepted (October 2026)

## Context
A document goes through several steps: upload, image clean-up, OCR, AI extraction,
post-processing and review. The steps always run in the same order, and one person builds and runs
the whole thing. Processing takes seconds, so it can't happen inside the upload request.

## Decision
Build one deployable app, split into projects with clear boundaries:

- `Core`: domain models and interfaces, with no infrastructure dependencies
- `Infrastructure`: OCR, AI providers, the pipeline, data access and storage
- `Worker`: the background processing service, as a class library
- `Api`: endpoints, SignalR, auth, and the host for the worker
- `Web`: the React app

The worker runs inside the API process and reads document ids from an in-memory queue
([ADR-0004](0004-channel-based-async.md)). Production is a single Docker image (API plus the built
React app) on Azure Container Apps. Local development uses .NET Aspire.

## Consequences
- One thing to build, deploy and debug, with no service discovery or messaging between services.
- The project boundaries keep the code honest, and the worker can move to its own app later by
  swapping the in-memory queue for a broker.
- The API and the worker scale together, and a crash loses whatever was queued in memory. On
  startup, documents left in `Processing` are picked up again.

## Alternatives considered
- **Microservices.** Too much operational overhead for one developer and a pipeline that always
  runs in order.
- **Processing inside the upload request.** Ties up HTTP connections for the length of an AI call.
- **Azure Functions.** Cold starts, and chaining the steps would need Durable Functions. Local
  development is also clumsier.

## Background reading
- [Advancing document processing using AI](https://newsletter.techworld-with-milan.com/p/advancing-document-processing-using-ai)
  (Tech World with Milan), the DocuSense write-up that shaped the pipeline, the dual OCR approach
  and calling the AI SDKs directly.
