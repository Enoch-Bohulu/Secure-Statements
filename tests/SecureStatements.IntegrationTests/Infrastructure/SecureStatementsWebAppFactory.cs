using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureStatements.IntegrationTests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the real API in-memory,
/// pointing it at a throwaway PostgreSQL container and a temporary blob directory, and
/// injecting deterministic JWT / download-token configuration so tests can mint valid tokens.
/// </summary>
public sealed class SecureStatementsWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _blobRootPath;

    public SecureStatementsWebAppFactory(string connectionString, string blobRootPath)
    {
        _connectionString = connectionString;
        _blobRootPath = blobRootPath;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting values are part of the host configuration and are therefore visible to
        // the configuration reads that Program.cs performs during its top-level statements
        // (e.g. binding JwtOptions and the connection string). AddInMemoryCollection via
        // ConfigureAppConfiguration would be applied too late for those early reads.
        builder.UseSetting("ConnectionStrings:Database", _connectionString);

        builder.UseSetting("Jwt:Issuer", TestConstants.JwtIssuer);
        builder.UseSetting("Jwt:Audience", TestConstants.JwtAudience);
        builder.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);

        builder.UseSetting("DownloadToken:SigningKey", TestConstants.DownloadTokenSigningKey);
        builder.UseSetting(
            "DownloadToken:LifetimeMinutes",
            TestConstants.DownloadTokenLifetimeMinutes.ToString());

        builder.UseSetting("BlobStore:RootPath", _blobRootPath);
    }
}



