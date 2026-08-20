namespace SecureStatements.IntegrationTests.Infrastructure;

/// <summary>
/// Configuration values shared between the test host and the token helpers. These MUST match
/// the values the <see cref="SecureStatementsWebAppFactory"/> injects into the API, so that
/// tokens minted here validate inside the running application.
/// </summary>
public static class TestConstants
{
    public const string JwtIssuer = "https://auth.integration.securestatements";
    public const string JwtAudience = "secure-statements-api-integration";
    public const string JwtSigningKey = "integration-test-jwt-signing-key-at-least-32chars";

    public const string DownloadTokenSigningKey = "integration-test-download-signing-key-32chars!!";
    public const int DownloadTokenLifetimeMinutes = 15;

    public const string AdminRole = "statements-admin";
}

