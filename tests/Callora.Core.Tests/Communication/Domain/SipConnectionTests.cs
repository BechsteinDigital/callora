using System;
using Callora.Plugin.Communication.Domain.Accounts;
using Xunit;

namespace Callora.Core.Tests.Communication.Domain;

/// <summary>
/// Invariants of the remodeled <see cref="SipConnection"/> auth (F5): each method carries only its
/// own fields, a registration requires an identity + expiry, and a trunk carries neither an expiry
/// nor (for IP auth) any credentials.
/// </summary>
public sealed class SipConnectionTests
{
    [Fact]
    public void Register_WithDigestAndExpiry_IsValid()
    {
        var connection = new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Register,
            new DigestAuthentication("alice", "auth-id", "secret://pw"), 3600);

        Assert.Equal(SipAuthMethod.Digest, connection.Authentication.Method);
        Assert.Equal(3600, connection.RegistrationExpirySeconds);
    }

    [Fact]
    public void Trunk_IpAuthenticated_HasNoCredentialsAndNoExpiry()
    {
        var connection = new SipConnection("h", 5060, SipTransport.Udp, SipAccountMode.Trunk,
            IpAuthentication.Instance, registrationExpirySeconds: null);

        Assert.Equal(SipAuthMethod.IpAuthenticated, connection.Authentication.Method);
        Assert.Null(connection.RegistrationExpirySeconds);
    }

    [Fact]
    public void Trunk_WithDigest_IsValid()
    {
        var connection = new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Trunk,
            new DigestAuthentication("u", null, "secret://pw"), registrationExpirySeconds: null);

        Assert.Equal(SipAuthMethod.Digest, connection.Authentication.Method);
    }

    [Fact]
    public void Trunk_WithMutualTls_IsValid()
    {
        var connection = new SipConnection("h", 5061, SipTransport.Tls, SipAccountMode.Trunk,
            new MutualTlsAuthentication("secret://client-cert"), registrationExpirySeconds: null);

        Assert.Equal(SipAuthMethod.MutualTls, connection.Authentication.Method);
    }

    [Fact]
    public void Register_WithIpAuthentication_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Register, IpAuthentication.Instance, 3600));
    }

    [Fact]
    public void Register_WithoutExpiry_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Register,
                new DigestAuthentication("u", null, "s"), registrationExpirySeconds: null));
    }

    [Fact]
    public void Trunk_WithExpiry_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SipConnection("h", 5060, SipTransport.Udp, SipAccountMode.Trunk, IpAuthentication.Instance, 60));
    }

    [Theory]
    [InlineData("", "s")]
    [InlineData("u", "")]
    public void Digest_BlankRequiredField_Throws(string username, string passwordSecretRef)
    {
        Assert.Throws<ArgumentException>(() => new DigestAuthentication(username, null, passwordSecretRef));
    }

    [Fact]
    public void MutualTls_BlankCertRef_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MutualTlsAuthentication("   "));
    }
}
