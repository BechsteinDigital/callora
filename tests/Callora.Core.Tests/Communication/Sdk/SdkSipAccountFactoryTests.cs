using System;
using System.Collections.Generic;
using System.Linq;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Xunit;
using SdkSipTransport = CalloraVoipSdk.Core.Domain.Lines.SipTransport;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// Mapping a persisted domain account to the SDK account the client registers with (B4-deep-2d-2):
/// digest fields map through, the password resolves via the plugin data protector, and unsupported
/// auth methods or unresolvable secrets are rejected rather than mapped to a half-valid account.
/// </summary>
public sealed class SdkSipAccountFactoryTests
{
    private const string PluginId = "communication";

    [Fact]
    public void Create_DigestRegisterAccount_MapsAllFields()
    {
        var factory = new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "s3cret")), PluginId);
        var account = DigestAccount(expiry: 600);

        var sdk = factory.Create(account);

        Assert.Equal("alice", sdk.Username);
        Assert.Equal("s3cret", sdk.Password);
        Assert.Equal("sip.example.com", sdk.SipServer);
        Assert.Equal(5060, sdk.Port);
        Assert.Equal(SdkSipTransport.Udp, sdk.Transport);
        Assert.Equal(600, sdk.RegistrationExpiry);
        Assert.Equal("Alice Line", sdk.DisplayName);
    }

    [Theory]
    [InlineData(SipTransport.Udp, SdkSipTransport.Udp)]
    [InlineData(SipTransport.Tcp, SdkSipTransport.Tcp)]
    [InlineData(SipTransport.Tls, SdkSipTransport.Tls)]
    public void Create_MapsTransport(SipTransport domainTransport, SdkSipTransport expected)
    {
        var factory = new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "s3cret")), PluginId);

        var sdk = factory.Create(DigestAccount(transport: domainTransport));

        Assert.Equal(expected, sdk.Transport);
    }

    [Fact]
    public void Create_DigestTrunkWithoutExpiry_UsesDefault()
    {
        var factory = new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "s3cret")), PluginId);
        var account = DigestAccount(mode: SipAccountMode.Trunk, expiry: null);

        var sdk = factory.Create(account);

        Assert.Equal(300, sdk.RegistrationExpiry);
    }

    [Fact]
    public void Create_NonDigestAuth_Throws()
    {
        var factory = new SdkSipAccountFactory(new FakePluginDataProtector(), PluginId);
        var connection = new SipConnection(
            "sip.example.com", 5060, SipTransport.Udp, SipAccountMode.Trunk, IpAuthentication.Instance, registrationExpirySeconds: null);
        var account = new SipAccount("acc-ip", "w1", "IP Trunk", connection, maxConcurrentCalls: 1, enabled: true);

        Assert.Throws<NotSupportedException>(() => factory.Create(account));
    }

    [Fact]
    public void Create_UnresolvableSecret_Throws()
    {
        var factory = new SdkSipAccountFactory(new FakePluginDataProtector(), PluginId); // no secrets registered

        Assert.Throws<InvalidOperationException>(() => factory.Create(DigestAccount()));
    }

    [Fact]
    public void Create_ResolvesSecret_UnderTheConfiguredPluginId()
    {
        var protector = new FakePluginDataProtector(("pw-ref", "s3cret"));
        var factory = new SdkSipAccountFactory(protector, PluginId);

        factory.Create(DigestAccount());

        Assert.Equal(PluginId, protector.LastPluginId);
    }

    private static SipAccount DigestAccount(
        SipTransport transport = SipTransport.Udp,
        SipAccountMode mode = SipAccountMode.Register,
        int? expiry = 600,
        string passwordRef = "pw-ref")
    {
        var auth = new DigestAuthentication("alice", authId: null, passwordSecretRef: passwordRef);
        var connection = new SipConnection("sip.example.com", 5060, transport, mode, auth, expiry);
        return new SipAccount("acc-1", "w1", "Alice Line", connection, maxConcurrentCalls: 2, enabled: true);
    }
}

/// <summary>A <see cref="IPluginDataProtector"/> double resolving a fixed set of references.</summary>
internal sealed class FakePluginDataProtector : IPluginDataProtector
{
    private readonly Dictionary<string, string> _secrets;

    public FakePluginDataProtector(params (string Ref, string Plain)[] secrets) =>
        _secrets = secrets.ToDictionary(s => s.Ref, s => s.Plain, StringComparer.Ordinal);

    public string? LastPluginId { get; private set; }

    public string Protect(string pluginId, string plaintext) => throw new NotSupportedException();

    public bool TryUnprotect(string pluginId, string protectedValue, out string plaintext)
    {
        LastPluginId = pluginId;
        return _secrets.TryGetValue(protectedValue, out plaintext!);
    }
}
