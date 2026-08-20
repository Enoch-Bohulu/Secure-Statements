using SecureStatements.Application.Abstractions;
using SecureStatements.Domain;

namespace SecureStatements.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IStatementRepository"/> that records interactions so tests can
/// assert both behavior (ownership filtering) and call ordering, without a database.
/// </summary>
public sealed class FakeStatementRepository : IStatementRepository
{
    private readonly List<Statement> _statements = new();

    /// <summary>Statements added via <see cref="AddAsync"/>, in insertion order.</summary>
    public IReadOnlyList<Statement> Added => _statements;

    /// <summary>Seeds a statement so it appears in queries without going through AddAsync.</summary>
    public FakeStatementRepository Seed(Statement statement)
    {
        _statements.Add(statement);
        return this;
    }

    public Task<IReadOnlyList<Statement>> ListByCustomerAsync(
        string customerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Statement> result = _statements
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Statement?> GetOwnedAsync(
        Guid statementId, string customerId, CancellationToken cancellationToken)
    {
        var match = _statements.FirstOrDefault(
            s => s.Id == statementId && s.CustomerId == customerId);

        return Task.FromResult(match);
    }

    public Task AddAsync(Statement statement, CancellationToken cancellationToken)
    {
        _statements.Add(statement);
        return Task.CompletedTask;
    }
}

