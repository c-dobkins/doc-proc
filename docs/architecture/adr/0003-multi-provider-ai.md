# ADR-0003: Abstract AI Providers Behind a Common Interface

## Status
Accepted, with implementation notes (October 2026)

## Context
The platform uses Large Language Models (LLMs) to understand OCR-extracted text and produce structured field data. Multiple AI providers exist with different characteristics:

| Provider | Strengths | Weaknesses |
|----------|-----------|------------|
| Azure OpenAI (GPT-4o) | Enterprise compliance, data residency, high accuracy | Cost, Azure dependency |
| OpenAI API | Latest models, structured output, broad documentation | Data sent to US, rate limits |
| Google Gemini | Long context window, competitive pricing | Newer ecosystem, less .NET support |
| Ollama (local) | Free, no data leaves machine, offline capable | Requires GPU, lower accuracy on complex docs |

We need to support switching between providers based on:
- Cost optimization (cheaper models for simple documents)
- Compliance requirements (some customers may require data to stay on-premises)
- Availability (failover if primary provider is down)
- Development/testing (use Ollama locally, cloud providers in production)

## Decision
Define a common **`IAiExtractionProvider`** interface in the Core project. Each provider implements this interface independently. An **`IAiProviderFactory`** selects the appropriate provider based on configuration, per-template override, or fallback logic.

```csharp
public interface IAiExtractionProvider
{
    string Name { get; }
    Task<ExtractionResult> ExtractAsync(string ocrText, ExtractionTemplate template, CancellationToken ct);
    Task<bool> IsAvailableAsync(CancellationToken ct);
}
```

All providers must:
1. Accept the same input (OCR text + template with prompt)
2. Return the same output (structured `ExtractionResult` with fields + confidence scores)
3. Enforce structured JSON output using provider-specific mechanisms
4. Track token usage and processing duration
5. Handle errors gracefully (wrap provider-specific exceptions)

## Consequences

### Positive
- Vendor independence: switch providers without changing business logic
- Cost optimization: route simple docs to cheaper models, complex docs to GPT-4o
- Resilience: circuit breaker can fall back to secondary provider
- Local development: use Ollama locally, no cloud costs during development
- Testability: mock the interface for unit tests

### Negative
- Lowest common denominator: provider-specific features (e.g., OpenAI's vision API for images) require interface extensions or bypass mechanisms
- Each new provider requires implementation and testing
- Prompt behavior varies across models — a prompt optimized for GPT-4o may perform differently on Gemini or Llama

### Neutral
- Prompt templates should be tested against each provider used in production
- Provider selection can be per-template (stored in ExtractionTemplate) or global (appsettings)

## Alternatives Considered

**Single provider (Azure OpenAI only)**
- Rejected: Vendor lock-in, no local development without Azure account, no cost optimization options.

**Semantic Kernel / LangChain abstraction**
- Rejected: Adds a heavyweight dependency layer. Our use case is simple (text in → structured JSON out) and doesn't need conversation memory, agents, or plugin orchestration. Direct SDK usage gives better control over structured output enforcement, token tracking, and error handling.

**Per-provider prompt templates**
- Deferred: For MVP, use a single prompt template per document type. If accuracy varies significantly across providers, add provider-specific prompt overrides later.

## References
- DocuSense article: Direct OpenAI SDK integration for production control
- OpenAI Structured Outputs: https://platform.openai.com/docs/guides/structured-outputs

## Implementation Notes (October 2026)

- Shared behaviour lives in `AiProviderBase`: prompt building, timing, JSON parsing, and wrapping
  errors in `AiExtractionException`. Each provider only implements the API call.
- `IAiProviderFactory` exposes only `GetDefaultProviderAsync`. It picks the configured default if
  available, otherwise the first available provider in `AiProviders:FallbackOrder`.
  **Per-template provider overrides and failure-triggered (circuit-breaker) fallback were not
  implemented.** Fallback is based on availability (configuration, and reachability for Ollama).
- Azure OpenAI is called through its OpenAI-compatible v1 endpoint with the OpenAI .NET SDK, so the
  Azure and OpenAI providers share one client library. `Azure.AI.OpenAI` is not used.
- Retries: Gemini and Ollama use `HttpClient` with the standard resilience handler from
  ServiceDefaults. The OpenAI SDK applies its own retry policy.
