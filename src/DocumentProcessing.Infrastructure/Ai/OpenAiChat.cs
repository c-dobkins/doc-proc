using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using OpenAI.Chat;

namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// Shared OpenAI-SDK plumbing for the OpenAI and Azure OpenAI providers. Azure OpenAI is called
/// through its OpenAI-compatible v1 endpoint (<c>{endpoint}/openai/v1/</c>), so both providers use
/// the same SDK and request shape, with no Azure-specific client library.
/// </summary>
internal static class OpenAiChat
{
    public static ChatClient CreateOpenAiClient(OpenAiOptions config, PipelineTransport? transport = null) =>
        new(config.Model, new ApiKeyCredential(config.ApiKey!), Options(endpoint: null, transport));

    public static ChatClient CreateAzureClient(AzureOpenAiOptions config, PipelineTransport? transport = null) =>
        new(config.DeploymentName, new ApiKeyCredential(config.ApiKey!), Options(AzureV1Endpoint(config.Endpoint!), transport));

    internal static Uri AzureV1Endpoint(string endpoint) => new($"{endpoint.TrimEnd('/')}/openai/v1/");

    public static async Task<(string ResponseText, int TokensUsed)> CompleteJsonAsync(
        ChatClient client, string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var response = await client.CompleteChatAsync(
            [new SystemChatMessage(systemPrompt), new UserChatMessage(userPrompt)],
            new ChatCompletionOptions
            {
                Temperature = 0.1f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            }, ct);

        var completion = response.Value;
        var responseText = completion.Content.Count > 0 ? completion.Content[0].Text : "{}";
        var tokensUsed = completion.Usage is null ? 0 : completion.Usage.InputTokenCount + completion.Usage.OutputTokenCount;

        return (responseText, tokensUsed);
    }

    private static OpenAIClientOptions Options(Uri? endpoint, PipelineTransport? transport)
    {
        var options = new OpenAIClientOptions();
        if (endpoint is not null)
            options.Endpoint = endpoint;
        if (transport is not null)
            options.Transport = transport;
        return options;
    }
}
