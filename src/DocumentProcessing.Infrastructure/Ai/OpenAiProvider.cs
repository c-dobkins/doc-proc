using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// AI extraction provider using the OpenAI API directly.
/// </summary>
public class OpenAiProvider(
    IOptions<AiProviderOptions> options,
    ILogger<OpenAiProvider> logger) : AiProviderBase(logger)
{
    private readonly OpenAiOptions _config = options.Value.OpenAi;
    private ChatClient? _client;

    public override string Name => "OpenAI";

    protected override Task<(string ResponseText, int TokensUsed)> CallApiAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        // ChatClient is thread-safe; create it once, on first use (the provider may be registered but unconfigured)
        _client ??= OpenAiChat.CreateOpenAiClient(_config);

        Logger.LogDebug("Sending extraction request to OpenAI model {Model}", _config.Model);

        return OpenAiChat.CompleteJsonAsync(_client, systemPrompt, userPrompt, ct);
    }

    public override Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        var available = _config.Enabled && !string.IsNullOrWhiteSpace(_config.ApiKey);
        return Task.FromResult(available);
    }
}
