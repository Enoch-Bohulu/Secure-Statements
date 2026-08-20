using FluentAssertions;
using SecureStatements.Domain;

namespace SecureStatements.UnitTests.Domain;

public sealed class AuditEntryTests
{
    [Fact]
    public void Construction_setsAllProvidedFields()
    {
        var when = DateTimeOffset.UtcNow;
        var statementId = Guid.NewGuid();

        var entry = new AuditEntry
        {
            OccurredAt = when,
            Action = AuditActions.Downloaded,
            CustomerId = "cust-1",
            StatementId = statementId,
            ClientIp = "10.0.0.1",
            Detail = "ok"
        };

        entry.OccurredAt.Should().Be(when);
        entry.Action.Should().Be("Downloaded");
        entry.CustomerId.Should().Be("cust-1");
        entry.StatementId.Should().Be(statementId);
        entry.ClientIp.Should().Be("10.0.0.1");
        entry.Detail.Should().Be("ok");
    }

    [Fact]
    public void OptionalFields_defaultToNull()
    {
        var entry = new AuditEntry
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = AuditActions.LinkIssued,
            CustomerId = "cust-1"
        };

        entry.StatementId.Should().BeNull();
        entry.ClientIp.Should().BeNull();
        entry.Detail.Should().BeNull();
    }
}

