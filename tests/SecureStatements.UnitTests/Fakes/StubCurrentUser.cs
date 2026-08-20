using SecureStatements.Application.Abstractions;

namespace SecureStatements.UnitTests.Fakes;

/// <summary>
/// Minimal <see cref="ICurrentUser"/> stub. Controllers only read these three values, so a
/// plain settable record is clearer here than a mocking framework.
/// </summary>
public sealed class StubCurrentUser : ICurrentUser
{
    public string? CustomerId { get; init; }
    public string? ClientIp { get; init; }
    public bool IsAuthenticated { get; init; }
}

