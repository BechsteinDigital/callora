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
    public void Create_DigestTrunk_SetsTrunkInboundFields()
    {
        var factory = new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "s3cret")), PluginId);
        var connection = new SipConnection(
            "sip.example.com", 5060, SipTransport.Tls, SipAccountMode.Trunk,
            new DigestAuthentication("alice", authId: null, passwordSecretRef: "pw-ref"), 600,
            outboundProxy: "proxy.example.com", inboundNumbers: ["+4930111", "+4930222"]);
        var account = new SipAccount("acc-trunk", "w1", "Credentialed Trunk", connection, maxConcurrentCalls: 4, enabled: true);

        var sdk = factory.Create(account);

        Assert.True(sdk.AcceptTrunkInbound);
        Assert.Equal("proxy.example.com", sdk.OutboundProxy);
        Assert.Equal(["+4930111", "+4930222"], sdk.InboundNumbers);
        Assert.Equal(600, sdk.RegistrationExpiry);
    }

    [Fact]
    public void Create_DigestRegister_DoesNotForceTrunkFields()
    {
        var factory = new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "s3cret")), PluginId);

        var sdk = factory.Create(DigestAccount(mode: SipAccountMode.Register));

        // A plain register account is left at the SDK defaults: no outbound proxy, no DID whitelist.
        Assert.Null(sdk.OutboundProxy);
        Assert.Null(sdk.InboundNumbers);
    }

    [Fact]
    public void Create_DigestRegister_DoesNotAcceptCallsAddressedToOtherUsers()
    {
        var factory = new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "s3cret")), PluginId);

        var sdk = factory.Create(DigestAccount(mode: SipAccountMode.Register));

        // Trunk inbound broadens matching beyond the account's own user: without a DID whitelist it
        // accepts anything addressed to the provider's domain. Two workspaces with accounts at the
        // same provider share that domain, so each would accept the other's calls — the workspace
        // boundary would depend on who answers first. A register account is 1:1 by definition and has
        // no reason to be broadened.
        Assert.False(sdk.AcceptTrunkInbound);
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
