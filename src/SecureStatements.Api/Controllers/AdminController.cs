using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureStatements.Api.Contracts;
using SecureStatements.Application.Services;

namespace SecureStatements.Api.Controllers;

// Statement ingestion, locked to the "statements-admin" role. Validates the upload, checks it's really a PDF, then hands off to the service to store it.
[ApiController]
[Route("admin/statements")]
[Authorize(Roles = "statements-admin")]
public sealed class AdminController : ControllerBase
{
    private const long MaxUploadBytes = 25L * 1024 * 1024;
    private static readonly byte[] PdfMagic = Encoding.ASCII.GetBytes("%PDF-");

    private readonly StatementService _statements;
    private readonly ILogger<AdminController> _logger;

    public AdminController(StatementService statements, ILogger<AdminController> logger)
    {
        _statements = statements;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<ActionResult<StatementSummaryDto>> Upload(
        [FromForm] UploadStatementRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var file = request.File!;
        if (file.Length <= 0 || file.Length > MaxUploadBytes)
        {
            ModelState.AddModelError(nameof(request.File), "File size is outside the allowed range.");
            return ValidationProblem(ModelState);
        }

        await using (var headerStream = file.OpenReadStream())
        {
            var header = new byte[PdfMagic.Length];
            var read = await ReadExactlyAsync(headerStream, header, cancellationToken);
            // Trust the bytes, not the file name.... reject anything that isn't a real PDF.
            if (read != header.Length || !header.AsSpan().SequenceEqual(PdfMagic))
            {
                ModelState.AddModelError(nameof(request.File), "Uploaded file is not a valid PDF.");
                return ValidationProblem(ModelState);
            }
        }

        await using var contentStream = file.OpenReadStream();
        var safeFileName = BuildSafeFileName(request.CustomerId, request.Period);

        var statement = await _statements.IngestAsync(
            request.CustomerId, request.Period, safeFileName,
            contentStream, file.Length, cancellationToken);

        _logger.LogInformation(
            "Ingested statement {StatementId} for customer {CustomerId}.",
            statement.Id, statement.CustomerId);

        return CreatedAtAction(
            actionName: null,
            routeValues: null,
            value: new StatementSummaryDto(
                statement.Id, statement.Period, statement.FileName,
                statement.SizeBytes, statement.CreatedAt));
    }

    // Strip everything except letters/digits (and dashes in the period) so customer text can't inject path or header characters into the file name.
    private static string BuildSafeFileName(string customerId, string period)
    {
        var safeCustomer = new string(customerId.Where(char.IsLetterOrDigit).ToArray());
        var safePeriod = new string(period.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return $"statement-{safeCustomer}-{safePeriod}.pdf";
    }

    private static async Task<int> ReadExactlyAsync(
        Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            // A single Read can return fewer bytes than asked, so loop until we've got them all.
            var read = await stream.ReadAsync(
                buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}