using DocumentProcessing.Api;
using DocumentProcessing.Api.DTOs;
using DocumentProcessing.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace DocumentProcessing.Tests;

public class SignalRProcessingNotifierTests
{
    [Fact]
    public async Task NotifyStatusChangedAsync_SendsSinglePayloadToDocumentGroup()
    {
        var documentId = Guid.NewGuid();
        var hubContext = Substitute.For<IHubContext<ProcessingHub>>();
        var groupProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Group(documentId.ToString()).Returns(groupProxy);
        var sut = new SignalRProcessingNotifier(hubContext);

        await sut.NotifyStatusChangedAsync(documentId, "Completed", TestContext.Current.CancellationToken);

        // SendAsync is an extension method over SendCoreAsync; the client handler
        // expects exactly one argument carrying both DocumentId and Status.
        await groupProxy.Received(1).SendCoreAsync(
            "StatusUpdated",
            Arg.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0] is StatusUpdatedMessage &&
                ((StatusUpdatedMessage)args[0]!).DocumentId == documentId &&
                ((StatusUpdatedMessage)args[0]!).Status == "Completed"),
            Arg.Any<CancellationToken>());
    }
}
