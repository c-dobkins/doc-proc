using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Threading.Channels;

namespace DocumentProcessing.Tests;

public class DocumentProcessingWorkerRecoveryTests
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChannelWriter<Guid> _channelWriter;

    public DocumentProcessingWorkerRecoveryTests()
    {
        _documentRepository = Substitute.For<IDocumentRepository>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IDocumentRepository)).Returns(_documentRepository);
        serviceProvider.GetService(typeof(IDocumentPipeline)).Returns(Substitute.For<IDocumentPipeline>());

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _channelWriter = Substitute.For<ChannelWriter<Guid>>();
        _channelWriter.WriteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
    }

    private DocumentProcessingWorker BuildWorker()
    {
        // Consumer reads from an empty channel that never produces items —
        // it will just wait for items until cancelled.
        var emptyChannel = Channel.CreateUnbounded<Guid>();
        return new DocumentProcessingWorker(
            emptyChannel.Reader,
            _channelWriter,
            _scopeFactory,
            Options.Create(new ProcessingOptions()),
            Substitute.For<ILogger<DocumentProcessingWorker>>());
    }

    [Fact]
    public async Task Recovery_StaleDocuments_ResetsStatusToUploaded()
    {
        var staleDoc = new Document { Id = Guid.NewGuid(), FileName = "stale.pdf", Status = DocumentStatus.Processing };
        _documentRepository
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), DocumentStatus.Processing, Arg.Any<CancellationToken>())
            .Returns([staleDoc]);

        using var cts = new CancellationTokenSource();
        var worker = BuildWorker();
        await worker.StartAsync(cts.Token);

        await Task.Delay(200, TestContext.Current.CancellationToken); // let recovery complete (all mock I/O, no real latency)
        await cts.CancelAsync();

        await _documentRepository.Received().UpdateAsync(
            Arg.Is<Document>(d => d.Id == staleDoc.Id && d.Status == DocumentStatus.Uploaded),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recovery_StaleDocuments_WritesEachIdToChannel()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _documentRepository
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), DocumentStatus.Processing, Arg.Any<CancellationToken>())
            .Returns([
                new Document { Id = id1, FileName = "doc1.pdf", Status = DocumentStatus.Processing },
                new Document { Id = id2, FileName = "doc2.pdf", Status = DocumentStatus.Processing }
            ]);

        using var cts = new CancellationTokenSource();
        var worker = BuildWorker();
        await worker.StartAsync(cts.Token);

        await Task.Delay(200, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await _channelWriter.Received().WriteAsync(id1, Arg.Any<CancellationToken>());
        await _channelWriter.Received().WriteAsync(id2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recovery_NoStaleDocuments_WritesNothingToChannel()
    {
        _documentRepository
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), DocumentStatus.Processing, Arg.Any<CancellationToken>())
            .Returns([]);

        using var cts = new CancellationTokenSource();
        var worker = BuildWorker();
        await worker.StartAsync(cts.Token);

        await Task.Delay(200, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await _channelWriter.DidNotReceive().WriteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recovery_WritesToChannelAfterResettingStatus()
    {
        // Write order matters: status must be reset before the ID is enqueued,
        // so a crash between the two doesn't leave a duplicate in a re-run.
        var staleId = Guid.NewGuid();
        var callOrder = new List<string>();

        _documentRepository
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), DocumentStatus.Processing, Arg.Any<CancellationToken>())
            .Returns([new Document { Id = staleId, FileName = "stale.pdf", Status = DocumentStatus.Processing }]);

        _documentRepository
            .When(r => r.UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>()))
            .Do(_ => callOrder.Add("update"));

        _channelWriter
            .When(w => w.WriteAsync(staleId, Arg.Any<CancellationToken>()))
            .Do(_ => callOrder.Add("write"));

        using var cts = new CancellationTokenSource();
        var worker = BuildWorker();
        await worker.StartAsync(cts.Token);

        await Task.Delay(200, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        Assert.Equal(["update", "write"], callOrder);
    }
}
