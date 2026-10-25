namespace DocumentProcessing.Core.Interfaces;

/// <summary>
/// Sends real-time processing status notifications to connected clients.
/// </summary>
public interface IProcessingNotifier
{
    Task NotifyStatusChangedAsync(Guid documentId, string status, CancellationToken ct);
}
