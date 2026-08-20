using SecureStatements.Application.Abstractions;
using SecureStatements.Domain;

namespace SecureStatements.Application.Services;

// Records security-relevant events. Thin on purpose: it stamps the entry with the injected clock and hands it to the repository to persist.
public sealed class AuditService
{
    private readonly IAuditRepository _auditRepository;
    private readonly IClock _clock;

    public AuditService(IAuditRepository auditRepository, IClock clock)
    {
        _auditRepository = auditRepository;
        _clock = clock;
    }

    public Task RecordAsync(
        string action,
        string customerId,
        Guid? statementId,
        string? clientIp,
        string? detail,
        CancellationToken cancellationToken)
    {
        var entry = new AuditEntry
        {
            OccurredAt = _clock.UtcNow,
            Action = action,
            CustomerId = customerId,
            StatementId = statementId,
            ClientIp = clientIp,
            Detail = detail
        };

        return _auditRepository.AddAsync(entry, cancellationToken);
    }
}