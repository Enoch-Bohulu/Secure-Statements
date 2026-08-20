using Microsoft.EntityFrameworkCore;
using SecureStatements.Application.Abstractions;
using SecureStatements.Domain;

namespace SecureStatements.Infrastructure.Persistence;

// EF Core statement repository. Reads use AsNoTracking since we never mutate what we fetch, and GetOwnedAsync filters by customer id right in the query.
public sealed class StatementRepository : IStatementRepository
{
    private readonly AppDbContext _db;

    public StatementRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Statement>> ListByCustomerAsync(
        string customerId, CancellationToken cancellationToken) =>
        await _db.Statements
            .AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Statement?> GetOwnedAsync(
        Guid statementId, string customerId, CancellationToken cancellationToken) =>
        _db.Statements
            .AsNoTracking()
            // Id AND customer id in one predicate.... this is the ownership check, done in SQL.
            .FirstOrDefaultAsync(
                s => s.Id == statementId && s.CustomerId == customerId,
                cancellationToken);

    public async Task AddAsync(Statement statement, CancellationToken cancellationToken)
    {
        _db.Statements.Add(statement);
        await _db.SaveChangesAsync(cancellationToken);
    }
}