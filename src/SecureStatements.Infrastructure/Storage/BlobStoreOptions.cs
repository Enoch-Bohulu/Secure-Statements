namespace SecureStatements.Infrastructure.Storage;

// Config for the filesystem blob store. Just the root folder where PDFs get written, bound from the "BlobStore" section.
public sealed class BlobStoreOptions
{
    public const string SectionName = "BlobStore";
    public string RootPath { get; set; } = string.Empty;
}