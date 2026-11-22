using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DocumentProcessing.Tests;

public class DocumentPipelineTests
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IBlobStorage _blobStorage;
    private readonly IPreProcessor _preProcessor;
    private readonly IOcrEngine _ocrEngine;
    private readonly IAiProviderFactory _aiProviderFactory;
    private readonly IPostProcessor _postProcessor;
    private readonly IResultStore _resultStore;
    private readonly DocumentPipeline _sut;

    private static readonly ProcessingOptions DefaultOptions = new()
    {
        AutoApproveThreshold = 90.0
    };

    public DocumentPipelineTests()
    {
        _documentRepository = Substitute.For<IDocumentRepository>();
        _blobStorage = Substitute.For<IBlobStorage>();
        _preProcessor = Substitute.For<IPreProcessor>();
        _ocrEngine = Substitute.For<IOcrEngine>();
        _aiProviderFactory = Substitute.For<IAiProviderFactory>();
        _postProcessor = Substitute.For<IPostProcessor>();
        _resultStore = Substitute.For<IResultStore>();
        var logger = Substitute.For<ILogger<DocumentPipeline>>();
        var options = Options.Create(DefaultOptions);

        _sut = new DocumentPipeline(
            _documentRepository, _blobStorage, _preProcessor, _ocrEngine,
            _aiProviderFactory, _postProcessor, _resultStore, options, logger);

        // Default: pre-processor always succeeds with a prepared stream
        _preProcessor.ProcessAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new PreProcessingResult
            {
                IsValid = true,
                PreparedPages = [new MemoryStream()],
                PageCount = 1
            }));
    }

    private static Document CreateDocument(ExtractionTemplate? template = null) => new()
    {
        Id = Guid.NewGuid(),
        FileName = "test.pdf",
        FileType = FileType.Pdf,
        StoragePath = "/uploads/test.pdf",
        Template = template,
        Status = DocumentStatus.Uploaded
    };

    private static OcrResult CreateOcrResult(long durationMs = 100) => new()
    {
        Text = "Extracted text from document",
        PageCount = 1,
        EngineUsed = "PdfPig",
        IsScanned = false,
        ProcessingTimeMs = durationMs
    };

    private static ExtractionResult CreateExtractionResult(double confidence, int tokens = 500, long durationMs = 2000) => new()
    {
        ProviderName = "TestProvider",
        OverallConfidence = confidence,
        TokensUsed = tokens,
        ProcessingTimeMs = durationMs,
        Fields = []
    };

    private static ProcessingResult CreateProcessingResult(double confidence) => new()
    {
        DocumentId = Guid.NewGuid(),
        ConfidenceScore = confidence,
        ExtractedFields = [],
        Metrics = new ProcessingMetrics()
    };

    private static ExtractionTemplate CreateTemplate() => new()
    {
        Name = "Invoice",
        PromptTemplate = "{ocrText}",
        Fields = []
    };

    // ── Document not found ────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_DocumentNotFound_ReturnsWithoutWritingStatus()
    {
        var id = Guid.NewGuid();
        _documentRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Document?)null);

        await _sut.ProcessAsync(id, CancellationToken.None);

        await _documentRepository.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    // ── Exception path ────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_OcrThrows_SetsStatusToFailed()
    {
        var document = CreateDocument(CreateTemplate());
        _documentRepository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        _blobStorage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Stream.Null);
        _ocrEngine.ExtractTextAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("OCR engine failure"));

        await _sut.ProcessAsync(document.Id, CancellationToken.None);

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.Status == DocumentStatus.Failed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_OcrThrows_SetsProcessedAt()
    {
        var document = CreateDocument(CreateTemplate());
        _documentRepository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        _blobStorage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Stream.Null);
        _ocrEngine.ExtractTextAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("OCR engine failure"));

        var before = DateTimeOffset.UtcNow;
        await _sut.ProcessAsync(document.Id, CancellationToken.None);

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.ProcessedAt >= before),
            Arg.Any<CancellationToken>());
    }

    // ── No template (OCR-only path) ───────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_NoTemplate_SkipsAiExtraction()
    {
        var document = CreateDocument(template: null);
        _documentRepository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        _blobStorage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Stream.Null);
        _ocrEngine.ExtractTextAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(CreateOcrResult());

        await _sut.ProcessAsync(document.Id, CancellationToken.None);

        await _aiProviderFactory.DidNotReceive().GetDefaultProviderAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_NoTemplate_SavesOcrOnlyResult()
    {
        var document = CreateDocument(template: null);
        var ocrResult = CreateOcrResult();
        _documentRepository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        _blobStorage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Stream.Null);
        _ocrEngine.ExtractTextAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ocrResult);

        await _sut.ProcessAsync(document.Id, CancellationToken.None);

        await _resultStore.Received().SaveResultAsync(
            Arg.Is<ProcessingResult>(r => r.RawOcrText == ocrResult.Text),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_NoTemplate_SetsStatusToNeedsReview()
    {
        var document = CreateDocument(template: null);
        _documentRepository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        _blobStorage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Stream.Null);
        _ocrEngine.ExtractTextAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(CreateOcrResult());

        await _sut.ProcessAsync(document.Id, CancellationToken.None);

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.Status == DocumentStatus.NeedsReview),
            Arg.Any<CancellationToken>());
    }

    // ── Confidence routing ────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_HighConfidence_SetsStatusToCompleted()
    {
        var document = CreateDocument(CreateTemplate());
        var options = Options.Create(new ProcessingOptions { AutoApproveThreshold = 90.0 });
        var pipeline = BuildPipeline(options);
        SetupHappyPath(document, confidence: 95.0);

        await pipeline.ProcessAsync(document.Id, CancellationToken.None);

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.Status == DocumentStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(85.0)]
    [InlineData(10.0)] // very low confidence still goes to a human, never auto-fails
    public async Task ProcessAsync_ConfidenceBelowAutoApprove_SetsStatusToNeedsReview(double confidence)
    {
        var document = CreateDocument(CreateTemplate());
        var options = Options.Create(new ProcessingOptions { AutoApproveThreshold = 90.0 });
        var pipeline = BuildPipeline(options);
        SetupHappyPath(document, confidence: confidence);

        await pipeline.ProcessAsync(document.Id, CancellationToken.None);

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.Status == DocumentStatus.NeedsReview),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_ConfidenceAtExactAutoApproveThreshold_SetsStatusToCompleted()
    {
        var document = CreateDocument(CreateTemplate());
        var options = Options.Create(new ProcessingOptions { AutoApproveThreshold = 90.0 });
        var pipeline = BuildPipeline(options);
        SetupHappyPath(document, confidence: 90.0);

        await pipeline.ProcessAsync(document.Id, CancellationToken.None);

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.Status == DocumentStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_ConfidenceJustBelowAutoApproveThreshold_SetsStatusToNeedsReview()
    {
        var document = CreateDocument(CreateTemplate());
        var options = Options.Create(new ProcessingOptions { AutoApproveThreshold = 90.0 });
        var pipeline = BuildPipeline(options);
        SetupHappyPath(document, confidence: 89.9);

        await pipeline.ProcessAsync(document.Id, CancellationToken.None);

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.Status == DocumentStatus.NeedsReview),
            Arg.Any<CancellationToken>());
    }

    // ── Metrics ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_PopulatesOcrAndAiMetrics()
    {
        var document = CreateDocument(CreateTemplate());
        SetupHappyPath(document, confidence: 95.0, ocrDurationMs: 150, aiDurationMs: 3000, tokens: 750);

        await _sut.ProcessAsync(document.Id, CancellationToken.None);

        await _resultStore.Received().SaveResultAsync(
            Arg.Is<ProcessingResult>(r =>
                r.Metrics.OcrDurationMs == 150 &&
                r.Metrics.AiDurationMs == 3000 &&
                r.Metrics.TokenCount == 750),
            Arg.Any<CancellationToken>());
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_CancelledBeforeOcr_ThrowsOperationCancelled()
    {
        var document = CreateDocument(CreateTemplate());
        _documentRepository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        _blobStorage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Stream.Null);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ocrEngine.ExtractTextAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ProcessAsync(document.Id, cts.Token));
    }

    // ── Pre-processing ────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_InvalidPreProcessing_SetsStatusToFailed()
    {
        var document = CreateDocument(CreateTemplate());
        _documentRepository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        _blobStorage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Stream.Null);
        _preProcessor.ProcessAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PreProcessingResult
            {
                IsValid = false,
                ValidationError = "Unsupported file type: application/zip"
            }));

        await _sut.ProcessAsync(document.Id, CancellationToken.None);

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.Status == DocumentStatus.Failed),
            Arg.Any<CancellationToken>());
        await _ocrEngine.DidNotReceive().ExtractTextAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DocumentPipeline BuildPipeline(IOptions<ProcessingOptions> options) =>
        new(_documentRepository, _blobStorage, _preProcessor, _ocrEngine, _aiProviderFactory,
            _postProcessor, _resultStore, options,
            Substitute.For<ILogger<DocumentPipeline>>());

    private void SetupHappyPath(
        Document document,
        double confidence,
        long ocrDurationMs = 100,
        long aiDurationMs = 2000,
        int tokens = 500)
    {
        _documentRepository.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        _blobStorage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Stream.Null);
        _ocrEngine.ExtractTextAsync(Arg.Any<Document>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(CreateOcrResult(ocrDurationMs));

        var aiProvider = Substitute.For<IAiExtractionProvider>();
        aiProvider.ExtractAsync(Arg.Any<string>(), Arg.Any<ExtractionTemplate>(), Arg.Any<CancellationToken>())
            .Returns(CreateExtractionResult(confidence, tokens, aiDurationMs));
        _aiProviderFactory.GetDefaultProviderAsync(Arg.Any<CancellationToken>()).Returns(aiProvider);

        var processingResult = CreateProcessingResult(confidence);
        processingResult.Metrics = new ProcessingMetrics
        {
            OcrDurationMs = ocrDurationMs,
            AiDurationMs = aiDurationMs,
            TokenCount = tokens
        };
        _postProcessor.ProcessAsync(Arg.Any<Document>(), Arg.Any<ExtractionResult>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(processingResult);
    }
}
