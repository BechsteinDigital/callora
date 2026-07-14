using Callora.Host.Backend.Infrastructure.Http;

namespace Callora.Host.Backend.Tests.Infrastructure.Http;

public sealed class ReservedHostRoutePrefixesTests
{
    [Theory]
    [InlineData("/api/auth")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/users/{userId}/data-export")]
    [InlineData("/api/security/rbac/roles")]
    [InlineData("/workspace/auth/login")]
    [InlineData("/API/AUTH/login")]
    [InlineData("/api/auth/")]
    [InlineData("api/auth/login")]
    public void Collides_ReservedNamespaces_ReturnTrue(string pathTemplate)
    {
        Assert.True(ReservedHostRoutePrefixes.Collides(pathTemplate));
    }

    [Theory]
    [InlineData("/api/calls")]
    [InlineData("/api/calls/{callId}/accept")]
    [InlineData("/api/authorizations")]
    [InlineData("/api/custom-widgets")]
    [InlineData("/api/test-plugin/ping")]
    [InlineData("")]
    [InlineData(null)]
    public void Collides_NonReservedRoutes_ReturnFalse(string? pathTemplate)
    {
        Assert.False(ReservedHostRoutePrefixes.Collides(pathTemplate));
    }
}
