using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// The platform must advertise exactly what the voice provider can connect (#111). An account
/// whose authentication method the provider cannot handle is refused at the edge with an
/// actionable reason, and an already-persisted one surfaces that reason instead of sitting on
/// "Connecting" forever.
/// </summary>
public sealed class SipAuthMethodSupportTests
{
    [Fact]
    public void OnlyDigest_IsSupported()
    {
        Assert.Equal([SipAuthMethod.Digest], SipAuthMethodSupport.Supported);
        Assert.True(SipAuthMethodSupport.IsSupported(SipAuthMethod.Digest));
        Assert.False(SipAuthMethodSupport.IsSupported(SipAuthMethod.IpAuthenticated));
        Assert.False(SipAuthMethodSupport.IsSupported(SipAuthMethod.MutualTls));
    }

    [Fact]
    public void SupportedMethod_HasNoRefusalReason()
    {
        Assert.Null(SipAuthMethodSupport.DescribeUnsupported(SipAuthMethod.Digest));
    }

    [Theory]
    [InlineData(SipAuthMethod.IpAuthenticated, "callora-voip-sdk#104")]
    [InlineData(SipAuthMethod.MutualTls, "callora-voip-sdk#183")]
    public void UnsupportedMethod_NamesTheUpstreamGap(SipAuthMethod method, string expectedReference)
    {
        var reason = SipAuthMethodSupport.DescribeUnsupported(method);

        Assert.NotNull(reason);
        // The message has to be actionable: an operator must learn what to use instead and
        // where the gap is tracked, not just that the request was refused.
        Assert.Contains(expectedReference, reason!, StringComparison.Ordinal);
        Assert.Contains("digest", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SipAuthMethod.IpAuthenticated)]
    [InlineData(SipAuthMethod.MutualTls)]
    public async Task PersistedUnsupportedAccount_FailsReconciliation_WithoutTouchingTheProvider(SipAuthMethod method)
    {
        // Accounts created before the edge guard existed still live in the database. They must
        // fail with the reason rather than be handed to a connector that cannot serve them.
        var connector = new FakeVoiceChannelConnector();
        var registry = new CommunicationChannelRegistry();
        var reconciler = new SipAccountRuntimeReconciler(
            connector,
            registry,
            new SdkCallAudioRegistrar(new SdkCallAudioStreamProvider(), NullLogger<SdkCallAudioRegistrar>.Instance),
            NullLogger<SipAccountRuntimeReconciler>.Instance);

        var result = await reconciler.ApplyAsync(Account("a1", "w1", method));

        Assert.Equal(SipRuntimeState.Failed, result.State);
        Assert.Equal(SipAuthMethodSupport.DescribeUnsupported(method), result.Error);
        Assert.Empty(registry.GetChannels("w1"));
        Assert.Equal(0, connector.ConnectCount("a1"));
    }

    [Fact]
    public async Task SupportedAccount_StillReachesTheProvider()
    {
        var connector = new FakeVoiceChannelConnector().Returns("a1", new FakeVoiceChannel { ChannelId = "a1" });
        var registry = new CommunicationChannelRegistry();
        var reconciler = new SipAccountRuntimeReconciler(
            connector,
            registry,
            new SdkCallAudioRegistrar(new SdkCallAudioStreamProvider(), NullLogger<SdkCallAudioRegistrar>.Instance),
            NullLogger<SipAccountRuntimeReconciler>.Instance);

        var result = await reconciler.ApplyAsync(Account("a1", "w1", SipAuthMethod.Digest));

        Assert.Equal(SipRuntimeState.Connected, result.State);
        Assert.Single(registry.GetChannels("w1"));
    }

    private static SipAccount Account(string id, string workspaceKey, SipAuthMethod method)
    {
        var (mode, authentication, expiry) = method switch
        {
            SipAuthMethod.IpAuthenticated =>
                (SipAccountMode.Trunk, (SipAuthentication)IpAuthentication.Instance, (int?)null),
            SipAuthMethod.MutualTls =>
                (SipAccountMode.Register, new MutualTlsAuthentication("secret-ref"), 300),
            _ =>
                (SipAccountMode.Register, new DigestAuthentication("alice", null, "secret-ref"), 300),
        };

        return new SipAccount(
            id,
            workspaceKey,
            $"Account {id}",
            new SipConnection("sip.example.com", 5060, SipTransport.Udp, mode, authentication, expiry),
            maxConcurrentCalls: 1,
            enabled: true);
    }
}
