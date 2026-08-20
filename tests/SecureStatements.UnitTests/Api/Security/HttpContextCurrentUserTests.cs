using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SecureStatements.Api.Security;

namespace SecureStatements.UnitTests.Api.Security;

public sealed class HttpContextCurrentUserTests
{
    private static HttpContextCurrentUser Build(HttpContext? context)
    {
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new HttpContextCurrentUser(accessor);
    }

    [Fact]
    public void CustomerId_readsNameIdentifierClaim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "cust-42") },
                authenticationType: "test"))
        };

        Build(context).CustomerId.Should().Be("cust-42");
    }

    [Fact]
    public void CustomerId_fallsBackToSubClaim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("sub", "cust-sub") },
                authenticationType: "test"))
        };

        Build(context).CustomerId.Should().Be("cust-sub");
    }

    [Fact]
    public void IsAuthenticated_isTrue_whenIdentityAuthenticated()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                Array.Empty<Claim>(), authenticationType: "test"))
        };

        Build(context).IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_isFalse_whenNoIdentity()
    {
        var context = new DefaultHttpContext(); // anonymous
        Build(context).IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void ClientIp_readsRemoteIpAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        Build(context).ClientIp.Should().Be("203.0.113.7");
    }

    [Fact]
    public void NullHttpContext_yieldsSafeDefaults()
    {
        var user = Build(context: null);

        user.CustomerId.Should().BeNull();
        user.ClientIp.Should().BeNull();
        user.IsAuthenticated.Should().BeFalse();
    }
}

