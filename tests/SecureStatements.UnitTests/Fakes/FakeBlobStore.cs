using SecureStatements.Application.Abstractions;

namespace SecureStatements.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IStatementBlobStore"/> keyed by generated GUIDs. Records the order
/// in which content was saved so tests can assert the blob is persisted before metadata.
/// </summary>
public sealed class FakeBlobStore : IStatementBlobStore
{
    private readonly Dictionary<string, byte[]> _blobs = new();

    /// <summary>Keys saved via <see cref="SaveAsync"/>, in call order.</summary>
    public List<string> SavedKeys { get; } = new();

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var key = Guid.NewGuid().ToString("N");
        _blobs[key] = buffer.ToArray();
        SavedKeys.Add(key);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        if (!_blobs.TryGetValue(key, out var bytes))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new MemoryStream(bytes, writable: false));
    }
}

