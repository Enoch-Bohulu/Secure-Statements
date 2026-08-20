using FluentAssertions;
using SecureStatements.Domain;

namespace SecureStatements.UnitTests.Domain;

public sealed class AuditActionsTests
{
    // These strings are persisted and may be queried by auditors/regulators. Changing a value
    // is a breaking change to historical data, so we pin the exact wire values.
    [Theory]
    [InlineData(AuditActions.LinkIssued, "LinkIssued")]
    [InlineData(AuditActions.LinkDenied, "LinkDenied")]
    [InlineData(AuditActions.Downloaded, "Downloaded")]
    [InlineData(AuditActions.DownloadDenied, "DownloadDenied")]
    [InlineData(AuditActions.StatementIngested, "StatementIngested")]
    public void Constants_haveStableWireValues(string actual, string expected)
    {
        actual.Should().Be(expected);
    }
}

