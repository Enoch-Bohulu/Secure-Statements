using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SecureStatements.Api.Contracts;

// The public shape of a statement we hand back to callers. Deliberately leaves out BlobKey and CustomerId so we never leak internal storage details.
public sealed record StatementSummaryDto(
    Guid Id,
    string Period,
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt);

// What we return when a customer asks for a download link: the URL to hit and when it stops working.
public sealed record DownloadLinkDto(string DownloadUrl, DateTimeOffset ExpiresAt);

// The multipart form the admin upload endpoint expects. The data-annotation attributes give us basic validation before any of our own code runs.
public sealed class UploadStatementRequest
{
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    [StringLength(32, MinimumLength = 1)]
    public string Period { get; set; } = string.Empty;

    [Required]
    public IFormFile? File { get; set; }
}