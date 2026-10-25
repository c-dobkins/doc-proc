using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DocumentProcessing.Tests;

public class IngestionHandlerTests
{
    private readonly IBlobStorage _blobStorage = Substitute.For<IBlobStorage>();
    private readonly IDocumentRepository _documentRepository = Substitute.For<IDocumentRepository>();
    private readonly IngestionHandler _sut;

    public IngestionHandlerTests()
    {
        _blobStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("2026/01/01/abc_invoice.pdf");
        _documentRepository.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Document>());
        _sut = new IngestionHandler(_blobStorage, _documentRepository, NullLogger<IngestionHandler>.Instance);
    }

    private static IngestionRequest Request(Guid? templateId = null) =>
        new(new MemoryStream([1, 2, 3]), "invoice.pdf", FileType.Pdf, 3, "upload", templateId);

    [Fact]
    public async Task IngestAsync_ValidRequest_StoresFileAndPersistsDocument()
    {
        var templateId = Guid.NewGuid();

        var document = await _sut.IngestAsync(Request(templateId), CancellationToken.None);

        Assert.Equal("invoice.pdf", document.FileName);
        Assert.Equal(FileType.Pdf, document.FileType);
        Assert.Equal(3, document.FileSizeBytes);
        Assert.Equal("upload", document.Source);
        Assert.Equal(templateId, document.TemplateId);
        Assert.Equal("2026/01/01/abc_invoice.pdf", document.StoragePath);
        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        await _documentRepository.Received(1).AddAsync(document, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestAsync_RepositoryFails_DeletesStoredFileAndRethrows()
    {
        _documentRepository.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.IngestAsync(Request(), CancellationToken.None));

        await _blobStorage.Received(1).DeleteAsync("2026/01/01/abc_invoice.pdf", Arg.Any<CancellationToken>());
    }
}
