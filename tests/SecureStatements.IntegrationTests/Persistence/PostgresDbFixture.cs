using Microsoft.EntityFrameworkCore;
using SecureStatements.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SecureStatements.IntegrationTests.Persistence;

/// <summary>
/// Starts a disposable PostgreSQL container and builds AppDbContext instances against it,
/// applying the real EF Core migrations. We use Postgres rather than SQLite here because the
/// repositories order by <see cref="DateTimeOffset"/>, which SQLite cannot translate in SQL —
/// and because testing persistence against the real engine is the whole point.
/// </summary>
public sealed class PostgresDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("securestatements_repo_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private DbContextOptions<AppDbContext> _options = null!;

    public AppDbContext CreateContext() => new(_options);

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .Options;

        await using var context = new AppDbContext(_options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();
}

/// <summary>Binds the repository test classes to a single shared Postgres container.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresDbCollection : ICollectionFixture<PostgresDbFixture>
{
    public const string Name = "postgres-db";
}

