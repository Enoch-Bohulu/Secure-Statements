using Testcontainers.PostgreSql;

namespace SecureStatements.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit fixture that starts a disposable PostgreSQL container once, boots the API against it,
/// and applies the real EF Core migrations on startup. Shared across a test collection so the
/// (relatively expensive) container and host start only once per run.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("securestatements_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly string _blobRootPath =
        Path.Combine(Path.GetTempPath(), "securestatements-it-" + Guid.NewGuid().ToString("N"));

    private SecureStatementsWebAppFactory? _factory;

    public SecureStatementsWebAppFactory Factory =>
        _factory ?? throw new InvalidOperationException("Fixture not initialized.");

    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        Directory.CreateDirectory(_blobRootPath);

        _factory = new SecureStatementsWebAppFactory(
            _database.GetConnectionString(), _blobRootPath);

        // Force host creation now so migrations run and any startup failure surfaces here.
        _ = _factory.Services;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _database.DisposeAsync();

        try
        {
            if (Directory.Exists(_blobRootPath))
            {
                Directory.Delete(_blobRootPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temporary blob directory.
        }
    }
}

/// <summary>Binds all integration test classes to the single shared <see cref="ApiFixture"/>.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

