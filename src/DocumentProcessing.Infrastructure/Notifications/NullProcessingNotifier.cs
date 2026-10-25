using DocumentProcessing.Core.Interfaces;

namespace DocumentProcessing.Infrastructure.Notifications;

/// <summary>
/// No-op notifier used when no real-time transport (e.g., SignalR) is configured.
/// Satisfies the IProcessingNotifier contract without side effects.
/// </summary>
public class NullProcessingNotifier : IProcessingNotifier
{
    public Task NotifyStatusChangedAsync(Guid documentId, string status, CancellationToken ct)
        => Task.CompletedTask;
}
