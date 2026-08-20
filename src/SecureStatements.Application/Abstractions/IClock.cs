namespace SecureStatements.Application.Abstractions;

// Wraps the system clock so time is injectable. Lets tests freeze or advance now and check expiry logic without actually waiting.
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}