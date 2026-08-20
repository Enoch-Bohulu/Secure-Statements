namespace SecureStatements.Domain;

// One immutable audit row for a security-relevant event (link issued, download, denial). We keep these because regulators need a trail of who did what.
public sealed class AuditEntry
{
    public long Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public required string Action { get; init; }
    public required string CustomerId { get; init; }
    public Guid? StatementId { get; init; }
    public string? ClientIp { get; init; }
    public string? Detail { get; init; }
}

// All the audit action names in one place, so a typo becomes a compile error instead of a silently wrong or unsearchable audit row.
public static class AuditActions
{
    public const string LinkIssued = "LinkIssued";
    public const string LinkDenied = "LinkDenied";
    public const string Downloaded = "Downloaded";
    public const string DownloadDenied = "DownloadDenied";
    public const string StatementIngested = "StatementIngested";
}