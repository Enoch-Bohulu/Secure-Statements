using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureStatements.Application.Options;
using SecureStatements.Application.Services;
using SecureStatements.UnitTests.Fakes;

namespace SecureStatements.UnitTests;

/// <summary>
/// Unit tests for <see cref="DownloadTokenService"/> — the security-critical component that
/// mints and verifies signed, time-limited download tokens. These tests assert the token is
/// tamper-proof, forgery-proof, and genuinely time-limited.
/// </summary>
public sealed class DownloadTokenServiceTests
{
    private const string SigningKey = "unit-test-download-token-signing-key-32chars-min";
    private const string OtherKey = "a-completely-different-signing-key-also-32chars!";
    private const int LifetimeMinutes = 15;

    private static readonly DateTimeOffset Now =
        new(2026, 08, 17, 12, 00, 00, TimeSpan.Zero);

    private static DownloadTokenService CreateService(
        out FixedClock clock, string signingKey = SigningKey, int lifetimeMinutes = LifetimeMinutes)
    {
        clock = new FixedClock(Now);
        var options = Options.Create(new DownloadTokenOptions
        {
            SigningKey = signingKey,
            LifetimeMinutes = lifetimeMinutes
        });

        return new DownloadTokenService(options, clock);
    }

    [Fact]
    public void Issue_then_Validate_round_trips_the_token_data()
    {
        var service = CreateService(out var clock);
        var statementId = Guid.NewGuid();
        const string customerId = "C123";

        var token = service.Issue(statementId, customerId);
        var data = service.Validate(token);

        data.Should().NotBeNull();
        data!.StatementId.Should().Be(statementId);
        data.CustomerId.Should().Be(customerId);

        // Expiry is carried as whole unix seconds, so compare at second resolution.
        var expected = DateTimeOffset.FromUnixTimeSeconds(
            clock.UtcNow.AddMinutes(LifetimeMinutes).ToUnixTimeSeconds());
        data.ExpiresAt.Should().Be(expected);
    }

    [Fact]
    public void Validate_rejects_a_tampered_payload()
    {
        var service = CreateService(out _);
        var token = service.Issue(Guid.NewGuid(), "C123");

        var parts = token.Split('.');
        var tamperedPayload = FlipFirstCharacter(parts[0]);
        var tampered = $"{tamperedPayload}.{parts[1]}";

        service.Validate(tampered).Should().BeNull();
    }

    [Fact]
    public void Validate_rejects_a_tampered_signature()
    {
        var service = CreateService(out _);
        var token = service.Issue(Guid.NewGuid(), "C123");

        var parts = token.Split('.');
        var tamperedSignature = FlipFirstCharacter(parts[1]);
        var tampered = $"{parts[0]}.{tamperedSignature}";

        service.Validate(tampered).Should().BeNull();
    }

    [Fact]
    public void Validate_rejects_a_token_forged_with_a_different_key()
    {
        var issuer = CreateService(out _, signingKey: SigningKey);
        var attackerValidator = CreateService(out _, signingKey: OtherKey);

        var token = issuer.Issue(Guid.NewGuid(), "C123");

        // A validator that does not hold the real signing key must reject the token.
        attackerValidator.Validate(token).Should().BeNull();
    }

    [Fact]
    public void Validate_rejects_an_expired_token()
    {
        var service = CreateService(out var clock);
        var token = service.Issue(Guid.NewGuid(), "C123");

        // Move time to exactly the expiry instant and beyond; both must be rejected.
        clock.Advance(TimeSpan.FromMinutes(LifetimeMinutes));
        service.Validate(token).Should().BeNull();

        clock.Advance(TimeSpan.FromSeconds(1));
        service.Validate(token).Should().BeNull();
    }

    [Fact]
    public void Validate_accepts_a_token_that_has_not_yet_expired()
    {
        var service = CreateService(out var clock);
        var token = service.Issue(Guid.NewGuid(), "C123");

        clock.Advance(TimeSpan.FromMinutes(LifetimeMinutes) - TimeSpan.FromSeconds(1));

        service.Validate(token).Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-separator")]
    [InlineData("too.many.parts")]
    [InlineData("!!!.@@@")]
    public void Validate_rejects_malformed_tokens(string? token)
    {
        var service = CreateService(out _);

        service.Validate(token!).Should().BeNull();
    }

    [Fact]
    public void Issue_throws_when_customer_id_is_blank()
    {
        var service = CreateService(out _);

        var act = () => service.Issue(Guid.NewGuid(), "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("")]
    public void Constructor_throws_when_signing_key_is_missing_or_too_short(string badKey)
    {
        var options = Options.Create(new DownloadTokenOptions
        {
            SigningKey = badKey,
            LifetimeMinutes = LifetimeMinutes
        });

        var act = () => new DownloadTokenService(options, new FixedClock(Now));

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_throws_when_lifetime_is_not_positive(int lifetimeMinutes)
    {
        var options = Options.Create(new DownloadTokenOptions
        {
            SigningKey = SigningKey,
            LifetimeMinutes = lifetimeMinutes
        });

        var act = () => new DownloadTokenService(options, new FixedClock(Now));

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Replaces the first character with a guaranteed-different base64url character.</summary>
    private static string FlipFirstCharacter(string value)
    {
        var replacement = value[0] == 'A' ? 'B' : 'A';
        return replacement + value[1..];
    }
}

