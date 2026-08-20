using SecureStatements.Domain;

namespace SecureStatements.Application.Abstractions;

// Port for writing audit rows. Kept tiny and append-only on purpose because audit records should never be updated or deleted after the fact.
public interface IAuditRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken cancellationToken);
}