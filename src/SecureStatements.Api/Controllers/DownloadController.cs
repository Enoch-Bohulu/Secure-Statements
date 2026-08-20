using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SecureStatements.Application.Abstractions;
using SecureStatements.Application.Services;
using SecureStatements.Domain;

namespace SecureStatements.Api.Controllers;

// Redeems a signed link and streams the PDF. Anonymous because a browser can't send a JWT, but it re-checks the token and ownership before serving a byte.
[ApiController]
[Route("download")]
[AllowAnonymous]
public sealed class DownloadController : ControllerBase
{
    private readonly DownloadTokenService _tokens;
    private readonly StatementService _statements;
    private readonly AuditService _audit;
    private readonly ICurrentUser _currentUser;

    public DownloadController(
        DownloadTokenService tokens,
        StatementService statements,
        AuditService audit,
        ICurrentUser currentUser)
    {
        _tokens = tokens;
        _statements = statements;
        _audit = audit;
        _currentUser = currentUser;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Download(string token, CancellationToken cancellationToken)
    {
        var data = _tokens.Validate(token);
        if (data is null)
        {
            return Unauthorized();
        }

        // Re-check ownership here too.... a valid token still can't grab someone else's file.
        var statement = await _statements.GetOwnedAsync(
            data.StatementId, data.CustomerId, cancellationToken);

        if (statement is null)
        {
            await _audit.RecordAsync(
                AuditActions.DownloadDenied, data.CustomerId, data.StatementId,
                _currentUser.ClientIp, "Statement not found or ownership mismatch.",
                cancellationToken);
            return NotFound();
        }

        var content = await _statements.OpenContentAsync(statement, cancellationToken);
        if (content is null)
        {
            await _audit.RecordAsync(
                AuditActions.DownloadDenied, data.CustomerId, data.StatementId,
                _currentUser.ClientIp, "Blob content missing.", cancellationToken);
            return NotFound();
        }

        await _audit.RecordAsync(
            AuditActions.Downloaded, data.CustomerId, data.StatementId,
            _currentUser.ClientIp, null, cancellationToken);

        // no-store keeps the PDF out of caches; nosniff stops the browser guessing content type.
        Response.Headers[HeaderNames.CacheControl] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(
            fileStream: content,
            contentType: "application/pdf",
            fileDownloadName: statement.FileName,
            enableRangeProcessing: false);
    }
}