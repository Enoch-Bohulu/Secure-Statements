namespace SecureStatements.Api.Security;

// Settings for validating incoming JWTs, bound from the "Jwt" section. We only check tokens here.... issuing them is somebody else's job.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    // Must be at least 32 chars. In production this comes from a secret manager, not config files.
    public string SigningKey { get; set; } = string.Empty;
}