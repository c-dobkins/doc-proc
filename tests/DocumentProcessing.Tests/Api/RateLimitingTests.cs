using System.Net;
using System.Net.Http.Headers;

namespace DocumentProcessing.Tests.Api;

public sealed class LowRateLimitApiFactory : ApiFactory
{
    protected override int ProcessingPermitsPerMinute => 2;
}

[Collection(ApiHostCollection.Name)]
public class RateLimitingTests(LowRateLimitApiFactory factory) : IClassFixture<LowRateLimitApiFactory>
{
    [Fact]
    public async Task Upload_BeyondPermitLimit_Returns429()
    {
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            var file = new ByteArrayContent("%PDF-1.7\n"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            var response = await client.PostAsync("/api/documents/upload", new MultipartFormDataContent { { file, "file", $"doc{i}.pdf" } }, TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests], statuses);
    }
}
