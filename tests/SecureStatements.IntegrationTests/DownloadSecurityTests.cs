using System.Net;
using FluentAssertions;
using SecureStatements.IntegrationTests.Infrastructure;

namespace SecureStatements.IntegrationTests;

/// <summary>
/// Verifies the download endpoint's token gate over the real HTTP pipeline: forged, malformed,
/// and expired tokens are all rejected with 401 before any content is served.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DownloadSecurityTests
{
    private readonly ApiFixture _fixture;

    public DownloadSecurityTests(ApiFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("not-a-valid-token")]
    [InlineData("tampered.signature")]
    [InlineData("YWJj.ZGVm")] // well-formed base64url but a bogus signature
    public async Task Download_with_a_forged_or_malformed_token_is_unauthorized(string token)
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/download/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Download_with_a_correctly_signed_but_expired_token_is_unauthorized()
    {
        // Correctly signed with the download key, but its expiry is in the past.
        var expiredToken = TestTokens.ExpiredDownloadToken(Guid.NewGuid(), "C-expired");
        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/download/{expiredToken}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

