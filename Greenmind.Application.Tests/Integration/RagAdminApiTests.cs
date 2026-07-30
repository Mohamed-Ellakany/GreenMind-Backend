using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Greenmind.Application.Contracts.Api;
using Greenmind.Domain.Consts;

namespace Greenmind.Application.Tests.Integration;

public sealed class RagAdminApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Theory]
    [InlineData("/api/admin/rag/ingest/quality-standard", 7)]
    [InlineData("/api/admin/rag/ingest/eu-regulation-body", 11)]
    [InlineData("/api/admin/rag/ingest/eu-regulation-annex", 3)]
    public async Task IngestionEndpoints_AcceptTheirMultipartContracts(
        string endpoint,
        int expectedChunks)
    {
        using var client = CreateClient(RoleNames.Admin);
        using var content = BuildRequest(endpoint);

        var response = await client.PostAsync(endpoint, content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<RagIngestionResponse>>();
        Assert.NotNull(payload?.Data);
        Assert.Equal(expectedChunks, payload!.Data!.ChunksIngested);
    }

    [Fact]
    public async Task IngestionEndpoints_RejectFarmOwnerAccounts()
    {
        using var client = CreateClient(RoleNames.FarmOwner);
        using var content = BuildRequest("/api/admin/rag/ingest/quality-standard");

        var response = await client.PostAsync(
            "/api/admin/rag/ingest/quality-standard",
            content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IngestionEndpoints_RejectNonPdfFiles()
    {
        using var client = CreateClient(RoleNames.Admin);
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("not a pdf"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, "File", "rules.txt");
        content.Add(new StringContent("Strawberry"), "Crop");
        content.Add(new StringContent("Invalid source"), "SourceName");

        var response = await client.PostAsync(
            "/api/admin/rag/ingest/quality-standard",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateClient(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-UserId", GreenmindApiFactory.AdminUserId);
        return client;
    }

    private static MultipartFormDataContent BuildRequest(string endpoint)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "File", "source.pdf");
        content.Add(new StringContent("Official source"), "SourceName");

        if (endpoint.EndsWith("quality-standard", StringComparison.Ordinal))
            content.Add(new StringContent("Strawberry"), "Crop");

        if (endpoint.EndsWith("eu-regulation-annex", StringComparison.Ordinal))
        {
            content.Add(new StringContent("ANNEX II"), "AnnexLabelHint");
            content.Add(new StringContent("Fragaria, strawberry"), "CropKeywordsCsv");
        }

        return content;
    }

    private sealed record RagIngestionResponse(int ChunksIngested);
}
