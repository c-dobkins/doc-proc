using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// AI extraction provider using Ollama for local open-source models.
/// </summary>
public class OllamaProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AiProviderOptions> options,
    ILogger<OllamaProvider> logger) : AiProviderBase(logger)
{
    private readonly OllamaOptions _config = options.Value.Ollama;

    public override string Name => "Ollama";

    protected override async Task<(string ResponseText, int TokensUsed)> CallApiAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var requestBody = new OllamaChatRequest
        {
            Model = _config.Model,
            Messages =
            [
                new OllamaChatMessage { Role = "system", Content = systemPrompt },
                new OllamaChatMessage { Role = "user", Content = userPrompt }
            ],
            Stream = false,
            Format = "json",
            Options = new OllamaRequestOptions { Temperature = 0.1 }
        };

        var client = httpClientFactory.CreateClient("Ollama");
        var url = $"{_config.BaseUrl}/api/chat";

        Logger.LogDebug("Sending extraction request to Ollama model {Model}", _config.Model);

        var response = await client.PostAsJsonAsync(url, requestBody, ct);
        response.EnsureSuccessStatusCode();

        var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(ct);

        var responseText = ollamaResponse?.Message?.Content ?? "{}";
        var tokensUsed = (ollamaResponse?.PromptEvalCount ?? 0) + (ollamaResponse?.EvalCount ?? 0);

        return (responseText, tokensUsed);
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        if (!_config.Enabled) return false;

        try
        {
            var client = httpClientFactory.CreateClient("Ollama");
            var response = await client.GetAsync($"{_config.BaseUrl}/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #region Ollama API DTOs

    private class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("options")]
        public OllamaRequestOptions? Options { get; set; }
    }

    private class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class OllamaRequestOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    private class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }

    #endregion
}
