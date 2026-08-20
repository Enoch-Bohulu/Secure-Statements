namespace SecureStatements.Application.Options;

// Config for the signed download links: the key we sign them with and how long they stay valid. Bound from the "DownloadToken" section.
public sealed class DownloadTokenOptions
{
    public const string SectionName = "DownloadToken";
    public string SigningKey { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 15;
}