# ADR-0003: AI providers behind one interface

## Status
Accepted (October 2026)

## Context
The AI step turns OCR text into the fields a template asks for. I want Azure OpenAI in
production, but I also want to run locally without a cloud bill (Ollama), try other models
(OpenAI, Gemini), and keep working if the main provider isn't configured or reachable.

## Decision
Define `IAiExtractionProvider` in `Core`:

```csharp
public interface IAiExtractionProvider
{
    string Name { get; }
    Task<ExtractionResult> ExtractAsync(string ocrText, ExtractionTemplate template, CancellationToken ct);
    Task<bool> IsAvailableAsync(CancellationToken ct);
}
```

`AiProviderBase` holds the shared work: building the prompt, timing, parsing the JSON response and
wrapping errors in `AiExtractionException`. Each provider only makes the API call.

- Azure OpenAI and OpenAI both use the OpenAI .NET SDK. Azure is called through its
  OpenAI-compatible v1 endpoint, so `Azure.AI.OpenAI` isn't needed.
- Gemini and Ollama use `HttpClient` with the standard resilience handler.
- `AiProviderFactory` returns the configured default if it's available, otherwise the first
  available provider in `AiProviders:FallbackOrder`.
- OCR text always goes in the user message, never in the system prompt.

## Consequences
- Switching providers is a configuration change, and tests can mock the interface.
- Fallback is based on availability (configured, and reachable for Ollama). A provider that fails
  mid-call doesn't trigger a fallback: the document is marked `Failed` and can be reprocessed.
- One prompt is shared by all models, so results vary between them.

## Alternatives considered
- **Azure OpenAI only.** Simpler, but no free local option.
- **Semantic Kernel or LangChain.** The job is text in, JSON out. Calling the SDKs directly gives
  tighter control over structured output, token counts and errors.
- **Per-template provider choice.** Not built. It can be added if one document type works better on
  a particular model.

## Background reading
- [Advancing document processing using AI](https://newsletter.techworld-with-milan.com/p/advancing-document-processing-using-ai)
  (Tech World with Milan), the DocuSense write-up that shaped the pipeline, the dual OCR approach
  and calling the AI SDKs directly.
