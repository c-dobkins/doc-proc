using Microsoft.AspNetCore.SignalR;

namespace DocumentProcessing.Api.Hubs;

/// <summary>
/// SignalR hub for real-time document processing status updates.
/// </summary>
public class ProcessingHub : Hub
{
    public async Task JoinDocumentGroup(string documentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, documentId);
    }

    public async Task LeaveDocumentGroup(string documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, documentId);
    }
}
