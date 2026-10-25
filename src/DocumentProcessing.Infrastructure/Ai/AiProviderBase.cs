using System.Diagnostics;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// Base class for all AI extraction providers.
/// Encodes the shared extraction flow — prompt building, timing, parsing, and error wrapping —
/// so each concrete provider only needs to implement the HTTP/SDK call itself.
///
/// Shared behaviour:
/// - Builds system + user prompts via PromptBuilder
/// - Times the end-to-end call
/// - Parses the response via ExtractionResponseParser
/// - Logs a warning when the provider returns the flat fallback format
/// - Wraps all provider exceptions in AiExtractionException for a uniform catch target
/// </summary>
public abstract class AiProviderBase(ILogger logger) : IAiExtractionProvider
{
    protected readonly ILogger Logger = logger;

    public abstract string Name { get; }

    public async Task<ExtractionResult> ExtractAsync(string ocrText, ExtractionTemplate template, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        var systemPrompt = PromptBuilder.BuildSystemPrompt();
        var userPrompt = PromptBuilder.Build(ocrText, template);

        try
        {
            var (responseText, tokensUsed) = await CallApiAsync(systemPrompt, userPrompt, ct);
            stopwatch.Stop();

            Logger.LogInformation(
                "{Provider} extraction completed: tokens={Tokens}, duration={Duration}ms",
                Name, tokensUsed, stopwatch.ElapsedMilliseconds);

            var result = ExtractionResponseParser.Parse(responseText, Name, stopwatch.ElapsedMilliseconds, tokensUsed);

            if (result.ParsedFormat == ResponseParseFormat.ParseFailed)
                Logger.LogWarning(
                    "{Provider} returned a response that could not be parsed as JSON. Raw snippet: {Snippet}",
                    Name, Snippet(responseText));
            else if (result.ParsedFormat == ResponseParseFormat.Flat)
                Logger.LogWarning(
                    "{Provider} returned flat format instead of the expected structured format. Raw snippet: {Snippet}",
                    Name, Snippet(responseText));

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AiExtractionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "{Provider} extraction failed after {Duration}ms", Name, stopwatch.ElapsedMilliseconds);
            throw new AiExtractionException($"AI provider '{Name}' failed during extraction: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes the provider-specific API call.
    /// Implementations must return the raw response text and the total token count.
    /// </summary>
    protected abstract Task<(string ResponseText, int TokensUsed)> CallApiAsync(
        string systemPrompt, string userPrompt, CancellationToken ct);

    public abstract Task<bool> IsAvailableAsync(CancellationToken ct);

    private static string Snippet(string text) =>
        text.Length > 200 ? string.Concat(text.AsSpan(0, 200), "...") : text;
}
