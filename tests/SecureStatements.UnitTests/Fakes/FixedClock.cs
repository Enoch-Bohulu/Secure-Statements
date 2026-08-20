using SecureStatements.Application.Abstractions;

namespace SecureStatements.UnitTests.Fakes;

/// <summary>
/// A deterministic <see cref="IClock"/> whose current time can be set explicitly and
/// advanced, enabling precise testing of time-dependent logic such as token expiry.
/// </summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now) => UtcNow = now;

    public DateTimeOffset UtcNow { get; set; }

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

