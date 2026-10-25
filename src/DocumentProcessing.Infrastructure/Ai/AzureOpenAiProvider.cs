using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// AI extraction provider using Azure OpenAI, via its OpenAI-compatible v1 endpoint.
/// </summary>
public class AzureOpenAiProvider(
    IOptions<AiProviderOptions> options,
    ILogger<AzureOpenAiProvider> logger) : AiProviderBase(logger)
{
    private readonly AzureOpenAiOptions _config = options.Value.AzureOpenAi;
    private ChatClient? _client;

    public override string Name => "AzureOpenAI";

    protected override Task<(string ResponseText, int TokensUsed)> CallApiAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        _client ??= OpenAiChat.CreateAzureClient(_config);

        Logger.LogDebug("Sending extraction request to Azure OpenAI deployment {Deployment}", _config.DeploymentName);

        return OpenAiChat.CompleteJsonAsync(_client, systemPrompt, userPrompt, ct);
    }

    public override Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        var available = _config.Enabled
            && !string.IsNullOrWhiteSpace(_config.Endpoint)
            && !string.IsNullOrWhiteSpace(_config.ApiKey);
        return Task.FromResult(available);
    }
}
