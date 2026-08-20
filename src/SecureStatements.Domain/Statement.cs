namespace SecureStatements.Domain;

// Metadata for one stored statement PDF. The actual bytes live in the blob store and we just keep a pointer to them here via BlobKey.
public sealed class Statement
{
    public Guid Id { get; init; }
    public required string CustomerId { get; init; }
    public required string Period { get; init; }
    public required string FileName { get; init; }
    public required string BlobKey { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}