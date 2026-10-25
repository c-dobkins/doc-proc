using DocumentProcessing.Api.DTOs;
using DocumentProcessing.Api.Hubs;
using DocumentProcessing.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace DocumentProcessing.Api;

/// <summary>
/// Sends real-time processing status updates to SignalR clients.
/// </summary>
public class SignalRProcessingNotifier(IHubContext<ProcessingHub> hubContext) : IProcessingNotifier
{
    public async Task NotifyStatusChangedAsync(Guid documentId, string status, CancellationToken ct)
    {
        await hubContext.Clients.Group(documentId.ToString())
            .SendAsync("StatusUpdated", new StatusUpdatedMessage(documentId, status), ct);
    }
}
