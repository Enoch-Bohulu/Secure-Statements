using SecureStatements.Application.Abstractions;
using SecureStatements.Domain;

namespace SecureStatements.Infrastructure.Persistence;

// EF Core audit repository. Just appends the row and saves.... audit is write-only, so there's nothing else to do here.
public sealed class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _db;

    public AuditRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        _db.AuditEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}