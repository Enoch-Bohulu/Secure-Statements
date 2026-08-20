using System.Net.Http.Headers;

namespace SecureStatements.IntegrationTests.Infrastructure;

/// <summary>Response shape for a statement summary returned by the API.</summary>
public sealed record StatementSummary(
    Guid Id, string Period, string FileName, long SizeBytes, DateTimeOffset CreatedAt);

/// <summary>Response shape for an issued download link.</summary>
public sealed record DownloadLink(string DownloadUrl, DateTimeOffset ExpiresAt);

/// <summary>Helpers for constructing request payloads used across integration tests.</summary>
public static class TestPayloads
{
    /// <summary>A minimal but valid PDF byte sequence, beginning with the "%PDF-" magic bytes.</summary>
    public static byte[] ValidPdfBytes(string marker = "hello") =>
        System.Text.Encoding.ASCII.GetBytes($"%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n{marker}\n%%EOF");

    /// <summary>Bytes that are deliberately not a PDF, to exercise content validation.</summary>
    public static byte[] NotPdfBytes() =>
        System.Text.Encoding.ASCII.GetBytes("this is definitely not a pdf file");

    /// <summary>
    /// Builds the multipart/form-data body the admin upload endpoint expects, with field names
    /// matching <c>UploadStatementRequest</c> (CustomerId, Period, File).
    /// </summary>
    public static MultipartFormDataContent Upload(string customerId, string period, byte[] fileBytes)
    {
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        return new MultipartFormDataContent
        {
            { new StringContent(customerId), "CustomerId" },
            { new StringContent(period), "Period" },
            { fileContent, "File", "statement.pdf" }
        };
    }
}

