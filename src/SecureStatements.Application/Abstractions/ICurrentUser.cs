namespace SecureStatements.Application.Abstractions;

// Who's calling, in framework-neutral terms. The API layer fills this from the HTTP context so services can read identity without depending on ASP.NET.
public interface ICurrentUser
{
    // null when the caller isn't authenticated or has no id claim.
    string? CustomerId { get; }

    string? ClientIp { get; }

    bool IsAuthenticated { get; }
}