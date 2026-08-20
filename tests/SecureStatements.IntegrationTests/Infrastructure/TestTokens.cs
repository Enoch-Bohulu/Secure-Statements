using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SecureStatements.IntegrationTests.Infrastructure;

/// <summary>
/// Mints tokens for tests so they can exercise protected endpoints without a real identity
/// provider. Produces both JWT bearer tokens (for authentication) and raw download tokens
/// (to test the signed-URL redemption path, including forged and expired cases).
/// </summary>
public static class TestTokens
{
    /// <summary>
    /// Creates a valid JWT bearer token for the given customer, optionally granting roles
    /// (for example the admin role required to ingest statements).
    /// </summary>
    public static string JwtFor(string customerId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, customerId) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestConstants.JwtIssuer,
            audience: TestConstants.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Builds a download token in the exact wire format the API uses, but already expired.
    /// It is correctly signed with the download signing key, so it isolates the expiry check:
    /// the API must reject it purely because its expiry is in the past.
    /// </summary>
    public static string ExpiredDownloadToken(Guid statementId, string customerId)
    {
        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        var payload = $"{statementId:N}|{customerId}|{expiredAt}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestConstants.DownloadTokenSigningKey));
        var signature = hmac.ComputeHash(payloadBytes);

        return $"{ToBase64Url(payloadBytes)}.{ToBase64Url(signature)}";
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

