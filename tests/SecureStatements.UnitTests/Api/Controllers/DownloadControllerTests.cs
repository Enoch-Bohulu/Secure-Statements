using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SecureStatements.Api.Controllers;
using SecureStatements.Application.Options;
using SecureStatements.Application.Services;
using SecureStatements.Domain;
using SecureStatements.UnitTests.Fakes;

namespace SecureStatements.UnitTests.Api.Controllers;

public sealed class DownloadControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed record Harness(
        DownloadController Controller,
        DownloadTokenService Tokens,
        FakeStatementRepository Repo,
        FakeBlobStore Blobs,
        RecordingAuditRepository Audit,
        FixedClock Clock);

    private static Harness Build()
    {
        var clock = new FixedClock(Now);
        var tokens = new DownloadTokenService(
            Options.Create(new DownloadTokenOptions { SigningKey = new string('k', 32), LifetimeMinutes = 15 }),
            clock);
        var repo = new FakeStatementRepository();
        var blobs = new FakeBlobStore();
        var statements = new StatementService(repo, blobs, clock);
        var audit = new RecordingAuditRepository();
        var auditService = new AuditService(audit, clock);
        var currentUser = new StubCurrentUser { ClientIp = "9.9.9.9" };

        var controller = new DownloadController(tokens, statements, auditService, currentUser)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return new Harness(controller, tokens, repo, blobs, audit, clock);
    }

    private static async Task<Statement> SeedOwnedWithBlob(Harness h, string customerId)
    {
        var key = await h.Blobs.SaveAsync(new MemoryStream("%PDF-1.4"u8.ToArray()), CancellationToken.None);
        var statement = new Statement
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Period = "2026-07",
            FileName = "s.pdf",
            BlobKey = key,
            SizeBytes = 8,
            CreatedAt = Now
        };
        h.Repo.Seed(statement);
        return statement;
    }

    [Fact]
    public async Task Download_garbageToken_returnsUnauthorized()
    {
        var h = Build();

        var result = await h.Controller.Download("not-a-token", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Download_expiredToken_returnsUnauthorized()
    {
        var h = Build();
        var statement = await SeedOwnedWithBlob(h, "cust-1");
        var token = h.Tokens.Issue(statement.Id, "cust-1");

        h.Clock.Advance(TimeSpan.FromMinutes(16)); // past the 15-minute lifetime

        var result = await h.Controller.Download(token, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Download_ownershipMismatch_returnsNotFound_andAuditsDenied()
    {
        var h = Build();
        // Token says cust-1, but no statement owned by cust-1 exists.
        var token = h.Tokens.Issue(Guid.NewGuid(), "cust-1");

        var result = await h.Controller.Download(token, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        h.Audit.Entries.Should().ContainSingle()
            .Which.Action.Should().Be(AuditActions.DownloadDenied);
    }

    [Fact]
    public async Task Download_blobMissing_returnsNotFound_andAuditsDenied()
    {
        var h = Build();
        // Statement exists and is owned, but its blob key points at nothing.
        var statement = new Statement
        {
            Id = Guid.NewGuid(),
            CustomerId = "cust-1",
            Period = "2026-07",
            FileName = "s.pdf",
            BlobKey = Guid.NewGuid().ToString("N"),
            SizeBytes = 1,
            CreatedAt = Now
        };
        h.Repo.Seed(statement);
        var token = h.Tokens.Issue(statement.Id, "cust-1");

        var result = await h.Controller.Download(token, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        h.Audit.Entries.Should().ContainSingle()
            .Which.Action.Should().Be(AuditActions.DownloadDenied);
    }

    [Fact]
    public async Task Download_valid_streamsPdf_setsSecurityHeaders_andAuditsDownloaded()
    {
        var h = Build();
        var statement = await SeedOwnedWithBlob(h, "cust-1");
        var token = h.Tokens.Issue(statement.Id, "cust-1");

        var result = await h.Controller.Download(token, CancellationToken.None);

        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be("s.pdf");

        var headers = h.Controller.Response.Headers;
        headers.CacheControl.ToString().Should().Contain("no-store");
        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");

        h.Audit.Entries.Should().ContainSingle()
            .Which.Action.Should().Be(AuditActions.Downloaded);
    }
}

