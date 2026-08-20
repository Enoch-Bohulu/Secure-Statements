using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureStatements.Api.Contracts;
using SecureStatements.Application.Abstractions;
using SecureStatements.Application.Services;
using SecureStatements.Domain;

namespace SecureStatements.Api.Controllers;

// Customer-facing endpoints: list your own statements and get a short-lived download link for one. Everything here is scoped to the caller's own customer id.
[ApiController]
[Route("statements")]
[Authorize]
public sealed class StatementsController : ControllerBase
{
    private readonly StatementService _statements;
    private readonly DownloadTokenService _tokens;
    private readonly AuditService _audit;
    private readonly ICurrentUser _currentUser;

    public StatementsController(
        StatementService statements,
        DownloadTokenService tokens,
        AuditService audit,
        ICurrentUser currentUser)
    {
        _statements = statements;
        _tokens = tokens;
        _audit = audit;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StatementSummaryDto>>> List(
        CancellationToken cancellationToken)
    {
        var customerId = _currentUser.CustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            return Forbid();
        }

        IReadOnlyList<Statement> items =
            await _statements.ListForCustomerAsync(customerId, cancellationToken);

        var result = items
            .Select(s => new StatementSummaryDto(
                s.Id, s.Period, s.FileName, s.SizeBytes, s.CreatedAt))
            .ToList();

        return Ok(result);
    }

    [HttpPost("{statementId:guid}/download-link")]
    public async Task<ActionResult<DownloadLinkDto>> CreateDownloadLink(
        Guid statementId, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.CustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            return Forbid();
        }

        var statement = await _statements.GetOwnedAsync(
            statementId, customerId, cancellationToken);

        if (statement is null)
        {
            await _audit.RecordAsync(
                AuditActions.LinkDenied, customerId, statementId, _currentUser.ClientIp,
                "Statement not found or not owned by requester.", cancellationToken);
            // 404 (not 403) so existence is not disclosed to a non-owner.
            return NotFound();
        }

        var token = _tokens.Issue(statement.Id, customerId);
        var validated = _tokens.Validate(token)
            ?? throw new InvalidOperationException("A freshly issued token failed validation.");

        var downloadUrl = Url.Action(
            action: nameof(DownloadController.Download),
            controller: "Download",
            values: new { token },
            protocol: Request.Scheme,
            host: Request.Host.Value)!;

        await _audit.RecordAsync(
            AuditActions.LinkIssued, customerId, statement.Id, _currentUser.ClientIp,
            null, cancellationToken);

        return Ok(new DownloadLinkDto(downloadUrl, validated.ExpiresAt));
    }
}