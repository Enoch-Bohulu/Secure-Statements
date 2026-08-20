using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureStatements.Application.Abstractions;
using SecureStatements.Infrastructure.Persistence;
using SecureStatements.Infrastructure.Storage;
using SecureStatements.Infrastructure.Time;

namespace SecureStatements.Infrastructure;

// One place to wire up all the Infrastructure services (db context, repositories, blob store, clock) and validate their config at startup.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BlobStoreOptions>()
            .Bind(configuration.GetSection(BlobStoreOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath),
                "BlobStore:RootPath is required.")
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Database' is not configured.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IStatementBlobStore, FileSystemBlobStore>();
        // Repos are scoped because the DbContext isn't thread-safe and lives per request.
        services.AddScoped<IStatementRepository, StatementRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        return services;
    }
}