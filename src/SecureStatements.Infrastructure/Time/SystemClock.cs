using SecureStatements.Application.Abstractions;

namespace SecureStatements.Infrastructure.Time;

// The real clock used in production, just handing back the current UTC time. Tests swap in a fake one instead so they can control time.
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}