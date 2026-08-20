using Microsoft.Extensions.Options;
using SecureStatements.Application.Abstractions;

namespace SecureStatements.Infrastructure.Storage;

// Stores PDF bytes on local disk. Keys are server-generated GUIDs, so no client input ever shapes a path, and every path is checked to stay inside the root.
public sealed class FileSystemBlobStore : IStatementBlobStore
{
    private const int StreamBufferSize = 81920;

    private readonly string _rootPath;

    public FileSystemBlobStore(IOptions<BlobStoreOptions> options)
    {
        var configuredPath = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("BlobStore:RootPath must be configured.");
        }

        _rootPath = Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N");
        var path = ResolveSafePath(key);

        // CreateNew fails if the file already exists, so we never silently overwrite a blob.
        await using var file = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            StreamBufferSize, useAsync: true);

        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolveSafePath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            StreamBufferSize, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    private string ResolveSafePath(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _))
        {
            throw new ArgumentException("Invalid blob key format.", nameof(key));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, key));

        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        // Belt and braces: even with GUID keys, make sure the resolved path can't escape the root.
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException("Resolved path escapes the storage root.", nameof(key));
        }

        return fullPath;
    }
}