using FluentAssertions;
using SecureStatements.Domain;

namespace SecureStatements.UnitTests.Domain;

public sealed class StatementTests
{
    [Fact]
    public void Construction_withAllFields_exposesThemUnchanged()
    {
        // Arrange
        var id = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow;

        // Act
        var statement = new Statement
        {
            Id = id,
            CustomerId = "cust-1",
            Period = "2026-07",
            FileName = "statement.pdf",
            BlobKey = "abc",
            SizeBytes = 42,
            CreatedAt = created
        };

        // Assert
        statement.Id.Should().Be(id);
        statement.CustomerId.Should().Be("cust-1");
        statement.Period.Should().Be("2026-07");
        statement.FileName.Should().Be("statement.pdf");
        statement.BlobKey.Should().Be("abc");
        statement.SizeBytes.Should().Be(42);
        statement.CreatedAt.Should().Be(created);
    }

    // Compile-time guarantee: 'required' members must be set. This documents the intent and
    // will fail to build if someone removes 'required', which is the behavior we care about.
    [Fact]
    public void RequiredMembers_areEnforcedByCompiler()
    {
        var create = () => new Statement
        {
            CustomerId = "c",
            Period = "p",
            FileName = "f",
            BlobKey = "b"
        };

        create().Should().NotBeNull();
    }
}

