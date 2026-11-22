# ADR-0004: In-memory Channel&lt;T&gt; as the work queue

## Status
Accepted (October 2026)

## Context
Processing a document takes 5 to 30 seconds, mostly waiting on the AI call. An upload should
return straight away, processing should run in the background with a limit on parallel work, and
the browser should hear about progress.

## Decision
The upload endpoint writes the document id to a bounded `Channel<Guid>` (capacity 1000, waits when
full). `DocumentProcessingWorker`, a `BackgroundService` in the API process, reads from it.

- One consumer loop starts up to `Processing:MaxConcurrency` documents at once (default 5), using
  a `SemaphoreSlim`.
- On shutdown it waits for documents in progress.
- On startup it re-queues any document still marked `Processing`.
- A failure sets the document to `Failed`. Users retry with **Reprocess**, and there is no
  dead-letter queue.
- Status changes go to the browser over SignalR.

## Consequences
- Nothing extra to install or pay for, and enqueueing is instant.
- The queue isn't durable. Ids queued when the process dies are lost, although documents already
  in `Processing` are recovered on restart.
- With several replicas, each processes only its own uploads. Azure Service Bus is the next step
  when that matters, and the worker code stays the same.

## Alternatives considered
- **Azure Service Bus or RabbitMQ.** Durable and able to scale out, but extra infrastructure and
  cost that this load doesn't need yet.
- **Hangfire.** Built for scheduled jobs rather than a stream of work, and it polls the database.
- **Polling the database.** Adds delay and load for no benefit here.
