using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using SecureStatements.IntegrationTests.Infrastructure;

namespace SecureStatements.IntegrationTests;

/// <summary>
/// Verifies the authentication and authorization gates on the API surface: anonymous access
/// to health, rejection of unauthenticated requests, and role enforcement on ingestion.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HealthAndAuthTests
{
    private readonly ApiFixture _fixture;

    public HealthAndAuthTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Health_endpoint_is_anonymous_and_returns_ok()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("healthy");
    }

    [Fact]
    public async Task Statements_without_a_token_is_unauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/statements");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Statements_with_a_valid_customer_token_is_ok()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.JwtFor("C-auth-ok"));

        var response = await client.GetAsync("/statements");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_upload_with_a_non_admin_token_is_forbidden()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.JwtFor("C-non-admin"));

        using var body = TestPayloads.Upload("C-non-admin", "2026-07", TestPayloads.ValidPdfBytes());
        var response = await client.PostAsync("/admin/statements", body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

