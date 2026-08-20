using SecureStatements.Domain;

namespace SecureStatements.Application.Abstractions;

// Port for statement metadata reads and writes. GetOwnedAsync takes the customer id on purpose so ownership is baked into the query, not bolted on later.
public interface IStatementRepository
{
    Task<IReadOnlyList<Statement>> ListByCustomerAsync(
        string customerId, CancellationToken cancellationToken);

    Task<Statement?> GetOwnedAsync(
        Guid statementId, string customerId, CancellationToken cancellationToken);

    Task AddAsync(Statement statement, CancellationToken cancellationToken);
}