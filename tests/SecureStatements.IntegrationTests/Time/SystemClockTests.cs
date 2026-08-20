using FluentAssertions;
using SecureStatements.Infrastructure.Time;

namespace SecureStatements.IntegrationTests.Time;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_isCloseToActualUtcNow_andInUtc()
    {
        var before = DateTimeOffset.UtcNow;
        var value = new SystemClock().UtcNow;
        var after = DateTimeOffset.UtcNow;

        value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        value.Offset.Should().Be(TimeSpan.Zero);
    }
}

