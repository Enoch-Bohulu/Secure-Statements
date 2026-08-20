using FluentAssertions;
using SecureStatements.Application.Services;
using SecureStatements.Domain;
using SecureStatements.UnitTests.Fakes;

namespace SecureStatements.UnitTests;

/// <summary>
/// Unit tests for <see cref="AuditService"/>, verifying that security-relevant events are
/// recorded with the correct fields and a clock-stamped timestamp.
/// </summary>
public sealed class AuditServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 08, 17, 14, 45, 30, TimeSpan.Zero);

    [Fact]
    public async Task RecordAsync_appends_an_entry_with_all_fields_and_the_clock_timestamp()
    {
        var repository = new RecordingAuditRepository();
        var service = new AuditService(repository, new FixedClock(Now));
        var statementId = Guid.NewGuid();

        await service.RecordAsync(
            action: AuditActions.LinkIssued,
            customerId: "C1",
            statementId: statementId,
            clientIp: "203.0.113.7",
            detail: "issued link",
            cancellationToken: CancellationToken.None);

        repository.Entries.Should().ContainSingle();
        var entry = repository.Entries.Single();
        entry.OccurredAt.Should().Be(Now);
        entry.Action.Should().Be(AuditActions.LinkIssued);
        entry.CustomerId.Should().Be("C1");
        entry.StatementId.Should().Be(statementId);
        entry.ClientIp.Should().Be("203.0.113.7");
        entry.Detail.Should().Be("issued link");
    }

    [Fact]
    public async Task RecordAsync_allows_null_statement_ip_and_detail()
    {
        var repository = new RecordingAuditRepository();
        var service = new AuditService(repository, new FixedClock(Now));

        await service.RecordAsync(
            AuditActions.LinkDenied, "C1", statementId: null,
            clientIp: null, detail: null, CancellationToken.None);

        var entry = repository.Entries.Single();
        entry.StatementId.Should().BeNull();
        entry.ClientIp.Should().BeNull();
        entry.Detail.Should().BeNull();
        entry.Action.Should().Be(AuditActions.LinkDenied);
    }
}

