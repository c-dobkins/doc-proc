# ADR-0004: Use Channel\<T\> for Async Document Processing

## Status
Accepted, with implementation notes (October 2026)

## Context
When a user uploads a document, the processing pipeline (OCR → AI extraction → post-processing) takes 5-30 seconds depending on document size and AI provider latency. This work cannot happen synchronously in the HTTP request thread — it would block the API and degrade the user experience.

We need a mechanism to:
1. Accept the upload quickly (< 1 second response)
2. Queue the document for background processing
3. Process documents asynchronously with configurable parallelism
4. Notify the frontend of progress in real-time

Options for the async work queue:
- .NET `Channel<T>` (in-memory bounded/unbounded channel)
- RabbitMQ / Azure Service Bus (external message broker)
- Hangfire / Quartz.NET (job scheduler)
- Database polling (query for unprocessed documents)

## Decision
Use **`System.Threading.Channels.Channel<T>`** as the in-process work queue, consumed by a .NET **`BackgroundService`**. The API enqueues document IDs to the channel after upload; the Worker dequeues and processes them through the pipeline.

Configuration:
- Bounded channel with capacity of 1000 (backpressure if queue is full)
- Configurable parallelism via multiple concurrent consumers (default: 5)
- On worker startup: scan for documents stuck in "Processing" status and re-enqueue them (crash recovery)

## Consequences

### Positive
- Zero infrastructure dependencies: no message broker to install, configure, or monitor
- Very fast: in-process, no network round-trips for enqueue/dequeue
- Built into .NET: no NuGet packages required, well-documented, production-proven
- Bounded channel provides natural backpressure
- Simple to understand and debug

### Negative
- **Messages are lost on process crash**: if the Worker dies while documents are queued in the channel, those document IDs are lost. Mitigated by startup recovery scan (re-enqueue documents stuck in "Processing" status).
- Not durable: no persistence, replay, or dead-letter queue built in. Must implement dead-letter logic at the application level.
- Single-process: cannot distribute work across multiple machines. If horizontal scaling of Workers is needed, must switch to an external broker.

### Neutral
- For MVP scale (dozens of documents/hour), Channel\<T\> is more than sufficient
- Migration path to Azure Service Bus or RabbitMQ is straightforward: replace the channel write/read with broker publish/subscribe, keeping the same Worker processing logic

## Alternatives Considered

**RabbitMQ**
- Rejected for MVP: Adds operational complexity (Docker container, management UI, connection handling). The durability and horizontal scaling benefits aren't needed at MVP scale. Clear upgrade path exists.

**Azure Service Bus**
- Rejected for MVP: Requires Azure account for local development, adds cost. Ideal for production scale-out — planned as the Channel replacement when moving to Azure.

**Hangfire**
- Rejected: Designed for scheduled/recurring jobs, not a streaming work queue. Adds database polling overhead. Dashboard is nice but not needed alongside our custom metrics.

**Database polling**
- Rejected: Polling introduces latency (up to polling interval) and unnecessary database load. Channel provides instant notification to consumers.

## References
- System.Threading.Channels: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
- Stephen Toub: "An Introduction to System.Threading.Channels"

## Implementation Notes (October 2026)

- The `BackgroundService` runs inside the API process (see ADR-0001), so the channel is shared
  in-process. `DocumentProcessingChannel` is bounded at 1000 with `FullMode = Wait`.
- Parallelism comes from a single consumer loop that starts up to `Processing:MaxConcurrency`
  documents at once (`SemaphoreSlim`, default 5), rather than multiple consumers.
- In-flight documents are awaited on shutdown. Documents left in `Processing` are re-enqueued on
  the next startup.
- There is no dead-letter mechanism: failures set the document to `Failed`, and users retry with
  **Reprocess**.
- With several replicas, each replica processes only its own uploads. Moving to Azure Service Bus
  is the planned step when that matters.
