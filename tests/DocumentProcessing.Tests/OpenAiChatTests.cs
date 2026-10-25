using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;
using DocumentProcessing.Infrastructure.Ai;

namespace DocumentProcessing.Tests;

/// <summary>
/// Verifies the HTTP requests the OpenAI SDK sends for both providers, without a network:
/// Azure OpenAI goes through its OpenAI-compatible v1 endpoint with the deployment as the model.
/// </summary>
public class OpenAiChatTests
{
    private const string CompletionJson = """
        {"id":"c1","object":"chat.completion","created":1,"model":"m",
         "choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"{\"fields\":[]}"}}],
         "usage":{"prompt_tokens":11,"completion_tokens":7,"total_tokens":18}}
        """;

    [Fact]
    public async Task CreateAzureClient_SendsToV1EndpointWithDeploymentAsModel()
    {
        var handler = new CapturingHandler(CompletionJson);
        var client = OpenAiChat.CreateAzureClient(
            new AzureOpenAiOptions { Endpoint = "https://contoso.openai.azure.com/", ApiKey = "azure-key", DeploymentName = "gpt-4.1-prod" },
            new HttpClientPipelineTransport(new HttpClient(handler)));

        var (text, tokens) = await OpenAiChat.CompleteJsonAsync(client, "system", "user", CancellationToken.None);

        Assert.Equal("https://contoso.openai.azure.com/openai/v1/chat/completions", handler.RequestUri!.ToString());
        Assert.Equal("Bearer azure-key", handler.Authorization);
        Assert.Equal("gpt-4.1-prod", handler.Body!.RootElement.GetProperty("model").GetString());
        Assert.Equal("json_object", handler.Body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Equal("{\"fields\":[]}", text);
        Assert.Equal(18, tokens);
    }

    [Fact]
    public async Task CreateOpenAiClient_SendsToOpenAiWithModel()
    {
        var handler = new CapturingHandler(CompletionJson);
        var client = OpenAiChat.CreateOpenAiClient(
            new OpenAiOptions { ApiKey = "sk-test", Model = "gpt-4.1" },
            new HttpClientPipelineTransport(new HttpClient(handler)));

        await OpenAiChat.CompleteJsonAsync(client, "system", "user", CancellationToken.None);

        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.RequestUri!.ToString());
        Assert.Equal("Bearer sk-test", handler.Authorization);
        Assert.Equal("gpt-4.1", handler.Body!.RootElement.GetProperty("model").GetString());
    }

    [Theory]
    [InlineData("https://contoso.openai.azure.com")]
    [InlineData("https://contoso.openai.azure.com/")]
    public void AzureV1Endpoint_NormalizesTrailingSlash(string endpoint)
    {
        Assert.Equal("https://contoso.openai.azure.com/openai/v1/", OpenAiChat.AzureV1Endpoint(endpoint).ToString());
    }

    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public JsonDocument? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
