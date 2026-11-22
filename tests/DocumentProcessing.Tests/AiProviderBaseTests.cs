using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Ai;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DocumentProcessing.Tests;

/// <summary>
/// Tests for the shared AiProviderBase extraction flow.
/// Uses StubAiProvider — a concrete in-test adapter — to control CallApiAsync responses
/// without touching any real HTTP client or SDK.
/// </summary>
public class AiProviderBaseTests
{
    private static ExtractionTemplate CreateTemplate() => new()
    {
        Name = "Invoice",
        PromptTemplate = "{ocrText}\n{fieldDefinitions}\n{outputFormat}",
        Fields = [new TemplateField { Name = "Total", DataType = FieldDataType.Currency, IsRequired = true }]
    };

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_ValidStructuredResponse_ReturnsParsedResult()
    {
        var json = """{"fields":[{"name":"Total","value":"100.00","confidence":90,"dataType":"Currency"}]}""";
        var provider = new StubAiProvider(Task.FromResult((json, 100)));

        var result = await provider.ExtractAsync("ocr text", CreateTemplate(), CancellationToken.None);

        Assert.Single(result.Fields);
        Assert.Equal("Total", result.Fields[0].Name);
        Assert.Equal(ResponseParseFormat.Structured, result.ParsedFormat);
        Assert.Equal("TestProvider", result.ProviderName);
    }

    [Fact]
    public async Task ExtractAsync_RecordsTokensAndProviderName()
    {
        var json = """{"fields":[]}""";
        var provider = new StubAiProvider(Task.FromResult((json, 250)));

        var result = await provider.ExtractAsync("ocr text", CreateTemplate(), CancellationToken.None);

        Assert.Equal(250, result.TokensUsed);
        Assert.Equal("TestProvider", result.ProviderName);
    }

    // ── Format warnings ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_FlatFormatResponse_StillParsesAndSetsFormat()
    {
        var json = """{"Total":{"value":"100.00","confidence":90}}""";
        var provider = new StubAiProvider(Task.FromResult((json, 50)));

        var result = await provider.ExtractAsync("ocr text", CreateTemplate(), CancellationToken.None);

        Assert.Equal(ResponseParseFormat.Flat, result.ParsedFormat);
        Assert.Single(result.Fields);
    }

    [Fact]
    public async Task ExtractAsync_ParseFailedResponse_ReturnsParseFailedResult()
    {
        var provider = new StubAiProvider(Task.FromResult(("not json at all", 30)));

        var result = await provider.ExtractAsync("ocr text", CreateTemplate(), CancellationToken.None);

        Assert.Equal(ResponseParseFormat.ParseFailed, result.ParsedFormat);
        Assert.Empty(result.Fields);
        Assert.Equal(0, result.OverallConfidence);
    }

    // ── Error wrapping ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_ApiThrowsHttpException_WrapsInAiExtractionException()
    {
        var provider = new StubAiProvider(
            Task.FromException<(string, int)>(new HttpRequestException("503 Service Unavailable")));

        var ex = await Assert.ThrowsAsync<AiExtractionException>(
            () => provider.ExtractAsync("ocr text", CreateTemplate(), CancellationToken.None));

        Assert.Contains("TestProvider", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task ExtractAsync_ApiThrowsOperationCancelled_IsNotWrapped()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = new StubAiProvider(
            Task.FromException<(string, int)>(new OperationCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.ExtractAsync("ocr text", CreateTemplate(), cts.Token));
    }

    [Fact]
    public async Task ExtractAsync_ApiThrowsAiExtractionException_IsNotDoubleWrapped()
    {
        var inner = new AiExtractionException("already wrapped", new Exception("original"));
        var provider = new StubAiProvider(Task.FromException<(string, int)>(inner));

        var ex = await Assert.ThrowsAsync<AiExtractionException>(
            () => provider.ExtractAsync("ocr text", CreateTemplate(), CancellationToken.None));

        Assert.Same(inner, ex); // exact same instance — not re-wrapped
    }

    // ── Stub adapter ──────────────────────────────────────────────────────────

    private sealed class StubAiProvider(Task<(string ResponseText, int TokensUsed)> apiResult)
        : AiProviderBase(Substitute.For<ILogger>())
    {
        public override string Name => "TestProvider";

        protected override Task<(string ResponseText, int TokensUsed)> CallApiAsync(
            string systemPrompt, string userPrompt, CancellationToken ct) => apiResult;

        public override Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);
    }
}
