using FluentAssertions;
using SecureStatements.Api.Security;
using SecureStatements.Application.Options;

namespace SecureStatements.UnitTests.Application.Options;

public sealed class OptionsDefaultsTests
{
    [Fact]
    public void DownloadTokenOptions_defaultsLifetimeTo15Minutes()
    {
        var options = new DownloadTokenOptions();

        options.LifetimeMinutes.Should().Be(15);
        options.SigningKey.Should().BeEmpty();
        DownloadTokenOptions.SectionName.Should().Be("DownloadToken");
    }

    [Fact]
    public void JwtOptions_defaultsAreEmptyAndSectionNameIsJwt()
    {
        var options = new JwtOptions();

        options.Issuer.Should().BeEmpty();
        options.Audience.Should().BeEmpty();
        options.SigningKey.Should().BeEmpty();
        JwtOptions.SectionName.Should().Be("Jwt");
    }
}

