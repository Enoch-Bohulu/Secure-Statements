using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SecureStatements.Api.Contracts;
using SecureStatements.Api.Controllers;
using SecureStatements.Application.Services;
using SecureStatements.UnitTests.Fakes;

namespace SecureStatements.UnitTests.Api.Controllers;

public sealed class AdminControllerTests
{
    private static readonly byte[] PdfBytes = "%PDF-1.4 body"u8.ToArray();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (AdminController controller, FakeBlobStore blobs, FakeStatementRepository repo)
        CreateController()
    {
        var blobs = new FakeBlobStore();
        var repo = new FakeStatementRepository();
        var service = new StatementService(repo, blobs, new FixedClock(Now));
        var controller = new AdminController(service, NullLogger<AdminController>.Instance);
        return (controller, blobs, repo);
    }

    private static void ForceModelError(AdminController controller) =>
        controller.ModelState.AddModelError("File", "required");

    [Fact]
    public async Task Upload_invalidModelState_returnsValidationProblem()
    {
        var (controller, _, _) = CreateController();
        ForceModelError(controller);

        var request = new UploadStatementRequest { CustomerId = "c", Period = "p" };

        var result = await controller.Upload(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task Upload_emptyFile_returnsValidationProblem()
    {
        var (controller, _, _) = CreateController();
        var request = new UploadStatementRequest
        {
            CustomerId = "cust-1",
            Period = "2026-07",
            File = new FakeFormFile(Array.Empty<byte>(), "empty.pdf")
        };

        var result = await controller.Upload(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task Upload_nonPdfContent_isRejectedByMagicByteCheck()
    {
        var (controller, blobs, repo) = CreateController();
        var request = new UploadStatementRequest
        {
            CustomerId = "cust-1",
            Period = "2026-07",
            File = new FakeFormFile("<html>not a pdf"u8.ToArray(), "evil.pdf")
        };

        var result = await controller.Upload(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
        blobs.SavedKeys.Should().BeEmpty("nothing should be stored when validation fails");
        repo.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_validPdf_storesAndReturnsSafeSummary()
    {
        var (controller, blobs, repo) = CreateController();
        var request = new UploadStatementRequest
        {
            CustomerId = "cust-1",
            Period = "2026-07",
            File = new FakeFormFile(PdfBytes, "whatever.pdf")
        };

        var result = await controller.Upload(request, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var summary = created.Value.Should().BeOfType<StatementSummaryDto>().Subject;

        blobs.SavedKeys.Should().HaveCount(1);
        repo.Added.Should().HaveCount(1);
        summary.CreatedAt.Should().Be(Now, "the ingest clock stamps CreatedAt");
    }

    [Fact]
    public async Task Upload_buildsSafeFileName_strippingUnsafeCharacters()
    {
        var (controller, _, repo) = CreateController();
        var request = new UploadStatementRequest
        {
            CustomerId = "cus/t\\..1",   // hostile characters
            Period = "2026-07",
            File = new FakeFormFile(PdfBytes, "ignored-by-server.pdf")
        };

        await controller.Upload(request, CancellationToken.None);

        var stored = repo.Added.Single();
        stored.FileName.Should().Be("statement-cust1-2026-07.pdf");
        stored.FileName.Should().NotContainAny("/", "\\", "..");
    }
}

