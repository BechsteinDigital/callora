using System;
using Callora.Plugin.Communication.Domain.Accounts;
using Xunit;

namespace Callora.Core.Tests.Communication.Domain;

/// <summary>
/// Invariants of the <see cref="SipConnection"/> auth: each method carries only its own fields, and
/// whether a connection registers is derived from the auth type, not the mode. Every connection
/// registers (and needs an expiry) except the registration-less IP-authenticated trunk, which carries
/// neither an expiry nor any credentials. A credentialed trunk registers and may carry trunk inbound
/// fields (outbound proxy, DID whitelist).
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
    public void Trunk_WithDigestAndExpiry_IsValid()
    {
        // A credentialed trunk registers, so it needs an expiry just like a register account.
        var connection = new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Trunk,
            new DigestAuthentication("u", null, "secret://pw"), 3600);

        Assert.Equal(SipAuthMethod.Digest, connection.Authentication.Method);
        Assert.Equal(3600, connection.RegistrationExpirySeconds);
    }

    [Fact]
    public void Trunk_WithDigestWithoutExpiry_Throws()
    {
        // Trunk + digest registers → an expiry is required (only Trunk + IP auth is registration-less).
        Assert.Throws<ArgumentException>(() =>
            new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Trunk,
                new DigestAuthentication("u", null, "secret://pw"), registrationExpirySeconds: null));
    }

    [Fact]
    public void Trunk_WithMutualTlsAndExpiry_IsValid()
    {
        var connection = new SipConnection("h", 5061, SipTransport.Tls, SipAccountMode.Trunk,
            new MutualTlsAuthentication("secret://client-cert"), 3600);

        Assert.Equal(SipAuthMethod.MutualTls, connection.Authentication.Method);
        Assert.Equal(3600, connection.RegistrationExpirySeconds);
    }

    [Fact]
    public void Trunk_WithDigest_StoresAndDefensivelyCopiesInboundFields()
    {
        var numbers = new List<string> { "+4930111", "  +4930222  ", "", "   " };
        var connection = new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Trunk,
            new DigestAuthentication("u", null, "secret://pw"), 3600,
            outboundProxy: "proxy.example.com", inboundNumbers: numbers);

        // Outbound proxy is preserved, blank DID entries are dropped and remaining ones trimmed.
        Assert.Equal("proxy.example.com", connection.OutboundProxy);
        Assert.Equal(["+4930111", "+4930222"], connection.InboundNumbers);

        // Mutating the source list afterwards does not affect the stored whitelist (defensive copy).
        numbers.Add("+4930999");
        Assert.Equal(2, connection.InboundNumbers.Count);
    }

    [Fact]
    public void OutboundProxy_Blank_NormalizesToNull()
    {
        var connection = new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Trunk,
            new DigestAuthentication("u", null, "secret://pw"), 3600, outboundProxy: "   ");

        Assert.Null(connection.OutboundProxy);
    }

    [Fact]
    public void InboundNumbers_DefaultsToEmpty_NeverNull()
    {
        var connection = new SipConnection("h", 5060, SipTransport.Tls, SipAccountMode.Register,
            new DigestAuthentication("u", null, "secret://pw"), 3600);

        Assert.Empty(connection.InboundNumbers);
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
