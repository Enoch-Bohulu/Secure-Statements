using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureStatements.Infrastructure.Storage;

namespace SecureStatements.IntegrationTests.Storage;

public sealed class FileSystemBlobStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemBlobStore _store;

    public FileSystemBlobStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ss-blob-tests", Guid.NewGuid().ToString("N"));
        _store = new FileSystemBlobStore(
            Options.Create(new BlobStoreOptions { RootPath = _root }));
    }

    [Fact]
    public async Task SaveAsync_returnsGuidKey_andRoundTripsBytes()
    {
        var payload = "%PDF-1.4 hello"u8.ToArray();

        var key = await _store.SaveAsync(new MemoryStream(payload), CancellationToken.None);

        Guid.TryParseExact(key, "N", out _).Should().BeTrue("keys are server-generated GUIDs");

        await using var read = await _store.OpenReadAsync(key, CancellationToken.None);
        read.Should().NotBeNull();
        using var buffer = new MemoryStream();
        await read!.CopyToAsync(buffer);
        buffer.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task OpenReadAsync_unknownButValidKey_returnsNull()
    {
        var missing = Guid.NewGuid().ToString("N");

        var stream = await _store.OpenReadAsync(missing, CancellationToken.None);

        stream.Should().BeNull();
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("....//....//secret")]
    public async Task OpenReadAsync_hostileOrMalformedKey_isRejected(string key)
    {
        var act = async () => await _store.OpenReadAsync(key, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>(
            "keys must be plain GUIDs and must resolve inside the storage root");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

