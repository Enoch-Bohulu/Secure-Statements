using System.Security.Claims;
using SecureStatements.Application.Abstractions;

namespace SecureStatements.Api.Security;

// The one place that turns HTTP claims into our framework-neutral ICurrentUser, so controllers and services never have to touch claims directly.
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    // Prefer the standard name-id claim, fall back to raw "sub" that some issuers use.
    public string? CustomerId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub");

    public string? ClientIp =>
        _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}