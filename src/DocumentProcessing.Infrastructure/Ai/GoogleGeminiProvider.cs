using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// AI extraction provider using Google Gemini REST API.
/// </summary>
public class GoogleGeminiProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AiProviderOptions> options,
    ILogger<GoogleGeminiProvider> logger) : AiProviderBase(logger)
{
    private readonly GeminiOptions _config = options.Value.Gemini;

    public override string Name => "Gemini";

    protected override async Task<(string ResponseText, int TokensUsed)> CallApiAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var requestBody = new GeminiRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Parts = [new GeminiPart { Text = $"{systemPrompt}\n\n{userPrompt}" }]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = 0.1,
                ResponseMimeType = "application/json"
            }
        };

        var client = httpClientFactory.CreateClient("Gemini");
        var url = $"{_config.BaseUrl}/v1beta/models/{_config.Model}:generateContent";

        // Send the key as a header, not a query parameter, so it never appears in logged/traced URLs
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("x-goog-api-key", _config.ApiKey);

        Logger.LogDebug("Sending extraction request to Gemini model {Model}", _config.Model);

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(ct);

        var responseText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "{}";
        var tokensUsed = (geminiResponse?.UsageMetadata?.PromptTokenCount ?? 0)
            + (geminiResponse?.UsageMetadata?.CandidatesTokenCount ?? 0);

        return (responseText, tokensUsed);
    }

    public override Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        var available = _config.Enabled && !string.IsNullOrWhiteSpace(_config.ApiKey);
        return Task.FromResult(available);
    }

    #region Gemini API DTOs

    private class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("responseMimeType")]
        public string? ResponseMimeType { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private class GeminiUsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }
    }

    #endregion
}
