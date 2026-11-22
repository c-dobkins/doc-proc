using System.Net;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DocumentProcessing.Tests;

public class GoogleGeminiProviderTests
{
    [Fact]
    public async Task ExtractAsync_SendsApiKeyAsHeader_NotInUrl()
    {
        var handler = new CapturingHandler(
            """{"candidates":[{"content":{"parts":[{"text":"{\"fields\":[]}"}]}}],"usageMetadata":{"promptTokenCount":3,"candidatesTokenCount":2}}""");
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Gemini").Returns(new HttpClient(handler));

        var options = Options.Create(new AiProviderOptions
        {
            DefaultProvider = "Gemini",
            Gemini = new GeminiOptions { Enabled = true, ApiKey = "secret-key", Model = "gemini-test" }
        });
        var sut = new GoogleGeminiProvider(factory, options, NullLogger<GoogleGeminiProvider>.Instance);

        var template = new ExtractionTemplate
        {
            Name = "Invoice",
            PromptTemplate = "{ocrText}\n{fieldDefinitions}\n{outputFormat}",
            Fields = [new TemplateField { Name = "Total", DataType = FieldDataType.Currency }]
        };

        var result = await sut.ExtractAsync("ocr", template, CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.DoesNotContain("secret-key", handler.Request!.RequestUri!.ToString());
        Assert.Equal("secret-key", Assert.Single(handler.Request.Headers.GetValues("x-goog-api-key")));
        Assert.Equal(5, result.TokensUsed);
    }

    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
