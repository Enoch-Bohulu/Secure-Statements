using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SecureStatements.Application.Abstractions;
using SecureStatements.Application.Options;

namespace SecureStatements.Application.Services;

// Mints and checks the signed, time-limited download tokens. Token is base64url(payload).base64url(HMAC), where payload is statementId, customerId and the expiry.
public sealed class DownloadTokenService
{
    private const char PartSeparator = '.';
    private const char FieldSeparator = '|';

    private readonly byte[] _signingKey;
    private readonly int _lifetimeMinutes;
    private readonly IClock _clock;

    public DownloadTokenService(IOptions<DownloadTokenOptions> options, IClock clock)
    {
        var value = options.Value;

        // Refuse to start with a weak key.... a short HMAC key makes tokens forgeable.
        if (string.IsNullOrWhiteSpace(value.SigningKey) || value.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "DownloadToken:SigningKey must be configured and at least 32 characters long.");
        }

        if (value.LifetimeMinutes <= 0)
        {
            throw new InvalidOperationException(
                "DownloadToken:LifetimeMinutes must be a positive number.");
        }

        _signingKey = Encoding.UTF8.GetBytes(value.SigningKey);
        _lifetimeMinutes = value.LifetimeMinutes;
        _clock = clock;
    }

    public sealed record TokenData(Guid StatementId, string CustomerId, DateTimeOffset ExpiresAt);

    public string Issue(Guid statementId, string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        // Expiry lives inside the signed payload, so nobody can extend a link by editing it.
        var expiresAt = _clock.UtcNow.AddMinutes(_lifetimeMinutes);
        var payload =
            $"{statementId:N}{FieldSeparator}{customerId}{FieldSeparator}{expiresAt.ToUnixTimeSeconds()}";

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = ComputeSignature(payloadBytes);

        return $"{ToBase64Url(payloadBytes)}{PartSeparator}{ToBase64Url(signature)}";
    }

    public TokenData? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split(PartSeparator);
        if (parts.Length != 2)
        {
            return null;
        }

        byte[] payloadBytes;
        byte[] providedSignature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            providedSignature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expectedSignature = ComputeSignature(payloadBytes);
        // Constant-time compare so response timing can't leak how close a forged signature was.
        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return null;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var fields = payload.Split(FieldSeparator);
        if (fields.Length != 3)
        {
            return null;
        }

        if (!Guid.TryParseExact(fields[0], "N", out var statementId))
        {
            return null;
        }

        var customerId = fields[1];
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        if (!long.TryParse(fields[2], out var expiryUnixSeconds))
        {
            return null;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiryUnixSeconds);
        // Reject on or after expiry, so the exact expiry second already counts as dead.
        if (_clock.UtcNow >= expiresAt)
        {
            return null;
        }

        return new TokenData(statementId, customerId, expiresAt);
    }

    private byte[] ComputeSignature(byte[] payloadBytes)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return hmac.ComputeHash(payloadBytes);
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2: normalized += "=="; break;
            case 3: normalized += "="; break;
            case 1: throw new FormatException("Invalid base64url string length.");
        }

        return Convert.FromBase64String(normalized);
    }
}