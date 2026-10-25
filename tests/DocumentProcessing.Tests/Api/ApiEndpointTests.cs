using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentProcessing.Api.DTOs;

namespace DocumentProcessing.Tests.Api;

/// <summary>
/// End-to-end tests of the HTTP surface: routing, binding, validation filters,
/// seed data and repository wiring, exercised through the real middleware pipeline.
/// </summary>
[Collection(ApiHostCollection.Name)]
public class ApiEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly byte[] MinimalPdfHeader = "%PDF-1.7\n%test\n"u8.ToArray();

    // ── Health & metrics ──────────────────────────────────────────────────────

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DashboardMetrics_CountsSeededDocuments()
    {
        var metrics = await _client.GetFromJsonAsync<DashboardMetricsDto>("/api/metrics/dashboard", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(metrics);
        Assert.True(metrics!.TotalDocuments >= 2);
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDocuments_ReturnsSeededDocumentsPaged()
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<DocumentDto>>("/api/documents?page=1&pageSize=10", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Contains(page!.Items, d => d.FileName == "sample-invoice.pdf");
        Assert.Contains(page.Items, d => d.FileName == "sample-receipt.pdf");
    }

    [Fact]
    public async Task GetDocumentById_Unknown_Returns404()
    {
        var response = await _client.GetAsync($"/api/documents/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ValidPdf_Returns202AndPersistsDocument()
    {
        var response = await _client.PostAsync("/api/documents/upload", FileForm("invoice.pdf", MinimalPdfHeader), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var upload = await response.Content.ReadFromJsonAsync<UploadDocumentResponse>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(upload);

        var detail = await _client.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{upload!.DocumentId}", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("invoice.pdf", detail!.FileName);
        Assert.Equal("Uploaded", detail.Status);

        var download = await _client.GetAsync($"/api/documents/{upload.DocumentId}/download", TestContext.Current.CancellationToken);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(MinimalPdfHeader, await download.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("empty.pdf", "")]
    [InlineData("notes.txt", "plain text")]
    [InlineData("fake.pdf", "<html>not really a pdf</html>")]
    public async Task Upload_InvalidFile_Returns400(string fileName, string content)
    {
        var response = await _client.PostAsync("/api/documents/upload", FileForm(fileName, System.Text.Encoding.UTF8.GetBytes(content)), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Approve_ExistingDocument_SetsStatusApproved()
    {
        var upload = await (await _client.PostAsync("/api/documents/upload", FileForm("approve-me.pdf", MinimalPdfHeader), TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UploadDocumentResponse>(cancellationToken: TestContext.Current.CancellationToken);

        var response = await _client.PostAsync($"/api/documents/{upload!.DocumentId}/approve", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await _client.GetFromJsonAsync<DocumentDetailDto>($"/api/documents/{upload.DocumentId}", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Approved", detail!.Status);
    }

    [Fact]
    public async Task UpdateFields_DocumentWithoutResult_Returns404()
    {
        var upload = await (await _client.PostAsync("/api/documents/upload", FileForm("no-result.pdf", MinimalPdfHeader), TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<UploadDocumentResponse>(cancellationToken: TestContext.Current.CancellationToken);

        var response = await _client.PutAsJsonAsync($"/api/documents/{upload!.DocumentId}/fields", new UpdateFieldsRequest([new FieldUpdateDto("Total", "1.00")]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Templates ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTemplates_ReturnsSeededTemplates()
    {
        var templates = await _client.GetFromJsonAsync<List<TemplateDto>>("/api/templates", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(templates);
        Assert.True(templates!.Count >= 2);
    }

    [Fact]
    public async Task CreateTemplate_InvalidRequest_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest("", null, [], ""), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Template_CreateUpdateDelete_RoundTrip()
    {
        var create = new CreateTemplateRequest(
            "Purchase Order",
            "Integration test template",
            [new TemplateFieldDto("PoNumber", "String", true, "PO number")],
            "Extract fields.\n{ocrText}\n{fieldDefinitions}\n{outputFormat}");

        var created = await _client.PostAsJsonAsync("/api/templates", create, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var template = await created.Content.ReadFromJsonAsync<TemplateDto>(cancellationToken: TestContext.Current.CancellationToken);

        var update = new UpdateTemplateRequest(
            "Purchase Order v2", create.Description, create.Fields, create.PromptTemplate);
        var updated = await (await _client.PutAsJsonAsync($"/api/templates/{template!.Id}", update, cancellationToken: TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<TemplateDto>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Purchase Order v2", updated!.Name);
        Assert.Equal(template.Version + 1, updated.Version);

        var deleted = await _client.DeleteAsync($"/api/templates/{template.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await _client.GetAsync($"/api/templates/{template.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    private static MultipartFormDataContent FileForm(string fileName, byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
