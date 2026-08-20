using SecureStatements.Application.Abstractions;
using SecureStatements.Domain;

namespace SecureStatements.Application.Services;

// The core statement use-cases (list, get, ingest). Every read goes through the customer id so one customer can never reach another's data.
public sealed class StatementService
{
    private readonly IStatementRepository _statements;
    private readonly IStatementBlobStore _blobStore;
    private readonly IClock _clock;

    public StatementService(
        IStatementRepository statements,
        IStatementBlobStore blobStore,
        IClock clock)
    {
        _statements = statements;
        _blobStore = blobStore;
        _clock = clock;
    }

    public Task<IReadOnlyList<Statement>> ListForCustomerAsync(
        string customerId, CancellationToken cancellationToken) =>
        _statements.ListByCustomerAsync(customerId, cancellationToken);

    public Task<Statement?> GetOwnedAsync(
        Guid statementId, string customerId, CancellationToken cancellationToken) =>
        _statements.GetOwnedAsync(statementId, customerId, cancellationToken);

    public Task<Stream?> OpenContentAsync(
        Statement statement, CancellationToken cancellationToken) =>
        _blobStore.OpenReadAsync(statement.BlobKey, cancellationToken);

    public async Task<Statement> IngestAsync(
        string customerId,
        string period,
        string fileName,
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        // Save the bytes first, then the row, so a statement never points at a missing blob.
        var blobKey = await _blobStore.SaveAsync(content, cancellationToken);

        var statement = new Statement
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Period = period,
            FileName = fileName,
            BlobKey = blobKey,
            SizeBytes = sizeBytes,
            CreatedAt = _clock.UtcNow
        };

        await _statements.AddAsync(statement, cancellationToken);
        return statement;
    }
}