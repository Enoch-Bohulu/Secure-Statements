using Microsoft.AspNetCore.Http;

namespace SecureStatements.UnitTests.Fakes;

/// <summary>
/// A minimal <see cref="IFormFile"/> over an in-memory byte array. Each OpenReadStream call
/// returns a fresh, independent stream — matching real form-file semantics and catching code
/// that wrongly assumes a single re-readable stream.
/// </summary>
public sealed class FakeFormFile : IFormFile
{
    private readonly byte[] _content;

    public FakeFormFile(byte[] content, string fileName, string contentType = "application/pdf")
    {
        _content = content;
        FileName = fileName;
        ContentType = contentType;
        Name = "File";
    }

    public string ContentType { get; }
    public string ContentDisposition => $"form-data; name=\"File\"; filename=\"{FileName}\"";
    public IHeaderDictionary Headers { get; } = new HeaderDictionary();
    public long Length => _content.Length;
    public string Name { get; }
    public string FileName { get; }

    public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
        target.WriteAsync(_content, 0, _content.Length, cancellationToken);

    public Stream OpenReadStream() => new MemoryStream(_content, writable: false);
}

