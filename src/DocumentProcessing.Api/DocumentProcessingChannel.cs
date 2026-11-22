using System.Threading.Channels;

namespace DocumentProcessing.Api;

/// <summary>
/// In-memory bounded channel for async document processing (ADR-0004).
/// Decouples upload API from background processing worker.
/// </summary>
public class DocumentProcessingChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelWriter<Guid> Writer => _channel.Writer;
    public ChannelReader<Guid> Reader => _channel.Reader;
}
