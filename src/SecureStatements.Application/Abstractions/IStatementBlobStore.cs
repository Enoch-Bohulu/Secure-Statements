namespace SecureStatements.Application.Abstractions;

// Port for the raw PDF bytes. Sits behind an interface so we can swap local disk for S3 or Azure Blob later without touching the core.
public interface IStatementBlobStore
{
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);
}