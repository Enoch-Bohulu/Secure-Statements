using SecureStatements.Application.Abstractions;
using SecureStatements.Domain;

namespace SecureStatements.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IAuditRepository"/> that keeps every appended entry so tests can
/// assert audit records are written with the expected fields.
/// </summary>
public sealed class RecordingAuditRepository : IAuditRepository
{
    public List<AuditEntry> Entries { get; } = new();

    public Task AddAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

