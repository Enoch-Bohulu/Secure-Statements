using System.Reflection;
using FluentAssertions;
using SecureStatements.Api.Contracts;

namespace SecureStatements.UnitTests.Api.Contracts;

public sealed class StatementSummaryDtoTests
{
    // Security regression guard: the public DTO must never leak internal storage details.
    // If someone adds BlobKey/CustomerId to the summary, this fails loudly.
    [Fact]
    public void Summary_doesNotExposeBlobKeyOrCustomerId()
    {
        var names = typeof(StatementSummaryDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToArray();

        names.Should().NotContain("BlobKey");
        names.Should().NotContain("CustomerId");
        names.Should().BeEquivalentTo(new[] { "Id", "Period", "FileName", "SizeBytes", "CreatedAt" });
    }
}

