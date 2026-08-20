using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using SecureStatements.IntegrationTests.Infrastructure;

namespace SecureStatements.IntegrationTests;

/// <summary>
/// Exercises the full customer journey against the real HTTP stack and database: an admin
/// ingests a statement, the owning customer lists it, requests a time-limited link, and
/// downloads the exact bytes. Also asserts content validation on ingestion.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StatementFlowTests
{
    private readonly ApiFixture _fixture;

    public StatementFlowTests(ApiFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(string customerId, params string[] roles)
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.JwtFor(customerId, roles));
        return client;
    }

    [Fact]
    public async Task Admin_can_ingest_a_pdf_and_the_owner_can_list_and_download_it()
    {
        var customerId = "C-" + Guid.NewGuid().ToString("N")[..8];
        var pdfBytes = TestPayloads.ValidPdfBytes(marker: customerId);

        // 1) Admin uploads a statement on behalf of the customer.
        var adminClient = ClientFor("admin-user", TestConstants.AdminRole);
        using var uploadBody = TestPayloads.Upload(customerId, "2026-07", pdfBytes);
        var uploadResponse = await adminClient.PostAsync("/admin/statements", uploadBody);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await uploadResponse.Content.ReadFromJsonAsync<StatementSummary>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();

        // 2) The owning customer sees exactly their statement.
        var customerClient = ClientFor(customerId);
        var listed = await customerClient.GetFromJsonAsync<List<StatementSummary>>("/statements");
        listed.Should().NotBeNull();
        listed!.Should().ContainSingle(s => s.Id == created.Id);

        // 3) The customer requests a time-limited download link.
        var linkResponse = await customerClient.PostAsync(
            $"/statements/{created.Id}/download-link", content: null);
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await linkResponse.Content.ReadFromJsonAsync<DownloadLink>();
        link.Should().NotBeNull();
        link!.DownloadUrl.Should().NotBeNullOrWhiteSpace();
        link.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        // 4) The link is redeemed anonymously and returns the exact PDF bytes.
        var downloadPath = new Uri(link.DownloadUrl).PathAndQuery;
        var anonymousClient = _fixture.CreateClient();
        var downloadResponse = await anonymousClient.GetAsync(downloadPath);

        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        downloadResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(pdfBytes);
    }

    [Fact]
    public async Task Admin_upload_of_a_non_pdf_file_is_rejected_with_bad_request()
    {
        var adminClient = ClientFor("admin-user", TestConstants.AdminRole);
        using var body = TestPayloads.Upload(
            "C-badfile", "2026-07", TestPayloads.NotPdfBytes());

        var response = await adminClient.PostAsync("/admin/statements", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Customer_cannot_get_a_download_link_for_another_customers_statement()
    {
        // Owner C-owner gets a statement ingested.
        var owner = "C-owner-" + Guid.NewGuid().ToString("N")[..6];
        var adminClient = ClientFor("admin-user", TestConstants.AdminRole);
        using var uploadBody = TestPayloads.Upload(owner, "2026-07", TestPayloads.ValidPdfBytes());
        var uploadResponse = await adminClient.PostAsync("/admin/statements", uploadBody);
        var created = await uploadResponse.Content.ReadFromJsonAsync<StatementSummary>();

        // A different customer tries to obtain a link for the owner's statement.
        var attackerClient = ClientFor("C-attacker-" + Guid.NewGuid().ToString("N")[..6]);
        var response = await attackerClient.PostAsync(
            $"/statements/{created!.Id}/download-link", content: null);

        // 404 (not 403) so existence is not disclosed to a non-owner.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

