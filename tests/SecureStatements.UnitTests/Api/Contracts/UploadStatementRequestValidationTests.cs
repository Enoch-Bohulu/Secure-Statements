using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using SecureStatements.Api.Contracts;
using SecureStatements.UnitTests.Fakes;

namespace SecureStatements.UnitTests.Api.Contracts;

public sealed class UploadStatementRequestValidationTests
{
    private static IList<ValidationResult> Validate(UploadStatementRequest request)
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_request_passesValidation()
    {
        var request = new UploadStatementRequest
        {
            CustomerId = "cust-1",
            Period = "2026-07",
            File = new FakeFormFile(new byte[] { 1 }, "a.pdf")
        };

        Validate(request).Should().BeEmpty();
    }

    [Fact]
    public void Missing_customerId_failsRequired()
    {
        var request = new UploadStatementRequest
        {
            CustomerId = "",
            Period = "2026-07",
            File = new FakeFormFile(new byte[] { 1 }, "a.pdf")
        };

        Validate(request).Should()
            .Contain(r => r.MemberNames.Contains(nameof(UploadStatementRequest.CustomerId)));
    }

    [Fact]
    public void CustomerId_over128Chars_failsStringLength()
    {
        var request = new UploadStatementRequest
        {
            CustomerId = new string('x', 129),
            Period = "2026-07",
            File = new FakeFormFile(new byte[] { 1 }, "a.pdf")
        };

        Validate(request).Should()
            .Contain(r => r.MemberNames.Contains(nameof(UploadStatementRequest.CustomerId)));
    }

    [Fact]
    public void Period_over32Chars_failsStringLength()
    {
        var request = new UploadStatementRequest
        {
            CustomerId = "cust-1",
            Period = new string('7', 33),
            File = new FakeFormFile(new byte[] { 1 }, "a.pdf")
        };

        Validate(request).Should()
            .Contain(r => r.MemberNames.Contains(nameof(UploadStatementRequest.Period)));
    }

    [Fact]
    public void Missing_file_failsRequired()
    {
        var request = new UploadStatementRequest
        {
            CustomerId = "cust-1",
            Period = "2026-07",
            File = null
        };

        Validate(request).Should()
            .Contain(r => r.MemberNames.Contains(nameof(UploadStatementRequest.File)));
    }
}

