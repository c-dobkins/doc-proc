using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentProcessing.Api.Auth;
using DocumentProcessing.Api.DTOs;
using Microsoft.AspNetCore.Hosting;

namespace DocumentProcessing.Tests.Api;

/// <summary>Production-like auth: writes need a key, reads stay public.</summary>
public sealed class KeyProtectedApiFactory : ApiFactory
{
    public const string ValidKey = "test-key-0123456789abcdef-xyz";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:RequireApiKeyForWrites", "true");
        builder.UseSetting("Auth:ApiKeys:0", ValidKey);
    }
}

[Collection(ApiHostCollection.Name)]
public class ApiKeyAuthTests(KeyProtectedApiFactory factory) : IClassFixture<KeyProtectedApiFactory>
{
    private HttpClient Client(string? key = null)
    {
        var client = factory.CreateClient();
        if (key is not null)
            client.DefaultRequestHeaders.Add(AuthPolicies.ApiKeyHeader, key);
        return client;
    }

    private static MultipartFormDataContent PdfForm()
    {
        var file = new ByteArrayContent("%PDF-1.7\n"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { file, "file", "secured.pdf" } };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key-0123456789abcdef-xyz")]
    public async Task Upload_WithoutValidKey_Returns401ProblemDetails(string? key)
    {
        var response = await Client(key).PostAsync("/api/documents/upload", PdfForm(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_WithValidKey_Returns202()
    {
        var response = await Client(KeyProtectedApiFactory.ValidKey).PostAsync("/api/documents/upload", PdfForm(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData("DELETE", "/api/templates/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/documents/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData("POST", "/api/documents/00000000-0000-0000-0000-000000000001/reprocess")]
    public async Task WriteEndpoints_WithoutKey_Return401(string method, string url)
    {
        var response = await Client().SendAsync(new HttpRequestMessage(new HttpMethod(method), url), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/documents?page=1&pageSize=5")]
    [InlineData("/api/templates")]
    [InlineData("/api/metrics/dashboard")]
    public async Task ReadEndpoints_WithoutKey_StayPublic(string url)
    {
        var response = await Client().GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("wrong-key-0123456789abcdef-xyz", false)]
    [InlineData(KeyProtectedApiFactory.ValidKey, true)]
    public async Task AuthStatus_ReportsWhetherCallerCanWrite(string? key, bool canWrite)
    {
        var status = await Client(key).GetFromJsonAsync<AuthStatusDto>("/api/auth/status", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new AuthStatusDto(WritesRequireKey: true, CanWrite: canWrite), status);
    }
}

/// <summary>Writes required but no key configured: the API must be read-only, not open.</summary>
public sealed class NoKeysConfiguredApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:RequireApiKeyForWrites", "true");
    }
}

[Collection(ApiHostCollection.Name)]
public class ApiKeyFailClosedTests(NoKeysConfiguredApiFactory factory) : IClassFixture<NoKeysConfiguredApiFactory>
{
    [Fact]
    public async Task Upload_NoKeysConfigured_Returns401()
    {
        var file = new ByteArrayContent("%PDF-1.7\n"u8.ToArray());
        var response = await factory.CreateClient().PostAsync("/api/documents/upload", new MultipartFormDataContent { { file, "file", "x.pdf" } }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
