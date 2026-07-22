using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// Provisioning enabled accounts into live channels (B4-deep-2d): each account is connected through
/// the connector seam, wrapped in an audio-registering decorator and registered under its workspace;
/// a connect failure skips only that account, and teardown deregisters and disposes everything.
/// </summary>
public sealed class VoiceChannelProvisionerTests
{
    [Fact]
    public async Task ProvisionAsync_ConnectsAndRegistersAllAccounts()
    {
        var registry = new CommunicationChannelRegistry();
        var connector = new FakeVoiceChannelConnector()
            .Returns("a1", new FakeVoiceChannel { ChannelId = "a1" })
            .Returns("a2", new FakeVoiceChannel { ChannelId = "a2" });
        var provisioner = NewProvisioner(connector, registry);

        var summary = await provisioner.ProvisionAsync([Account("a1", "w1"), Account("a2", "w1")]);

        Assert.Equal(new VoiceProvisioningSummary(2, 2), summary);
        var channels = registry.GetChannels("w1");
        Assert.Equal(2, channels.Count);
        Assert.All(channels, c => Assert.IsType<AudioRegisteringChannel>(c));
    }

    [Fact]
    public async Task ProvisionAsync_RegistersUnderEachAccountWorkspace()
    {
        var registry = new CommunicationChannelRegistry();
        var connector = new FakeVoiceChannelConnector()
            .Returns("a1", new FakeVoiceChannel { ChannelId = "a1" })
            .Returns("a2", new FakeVoiceChannel { ChannelId = "a2" });
        var provisioner = NewProvisioner(connector, registry);

        await provisioner.ProvisionAsync([Account("a1", "wA"), Account("a2", "wB")]);

        Assert.Equal("a1", Assert.Single(registry.GetChannels("wA")).ChannelId);
        Assert.Equal("a2", Assert.Single(registry.GetChannels("wB")).ChannelId);
    }

    [Fact]
    public async Task ProvisionAsync_SkipsAccountsThatDoNotConnect()
    {
        var registry = new CommunicationChannelRegistry();
        var connector = new FakeVoiceChannelConnector()
            .Returns("a1", new FakeVoiceChannel { ChannelId = "a1" })
            .Returns("a2", channel: null);
        var provisioner = NewProvisioner(connector, registry);

        var summary = await provisioner.ProvisionAsync([Account("a1", "w1"), Account("a2", "w1")]);

        Assert.Equal(new VoiceProvisioningSummary(2, 1), summary);
        Assert.Equal("a1", Assert.Single(registry.GetChannels("w1")).ChannelId);
    }

    [Fact]
    public async Task ProvisionAsync_ContinuesWhenAConnectThrows()
    {
        var registry = new CommunicationChannelRegistry();
        var connector = new FakeVoiceChannelConnector()
            .Throws("a1")
            .Returns("a2", new FakeVoiceChannel { ChannelId = "a2" });
        var provisioner = NewProvisioner(connector, registry);

        var summary = await provisioner.ProvisionAsync([Account("a1", "w1"), Account("a2", "w1")]);

        Assert.Equal(new VoiceProvisioningSummary(2, 1), summary);
        Assert.Equal("a2", Assert.Single(registry.GetChannels("w1")).ChannelId);
    }

    [Fact]
    public async Task Teardown_DeregistersAndDisposesChannels()
    {
        var registry = new CommunicationChannelRegistry();
        var innerA = new FakeVoiceChannel { ChannelId = "a1" };
        var innerB = new FakeVoiceChannel { ChannelId = "a2" };
        var connector = new FakeVoiceChannelConnector().Returns("a1", innerA).Returns("a2", innerB);
        var provisioner = NewProvisioner(connector, registry);
        await provisioner.ProvisionAsync([Account("a1", "w1"), Account("a2", "w1")]);

        provisioner.Teardown();

        Assert.Empty(registry.GetChannels("w1"));
        Assert.True(innerA.Disposed);
        Assert.True(innerB.Disposed);
    }

    private static VoiceChannelProvisioner NewProvisioner(
        IVoiceChannelConnector connector,
        CommunicationChannelRegistry registry) =>
        new(
            connector,
            registry,
            new SdkCallAudioRegistrar(new SdkCallAudioStreamProvider(), NullLogger<SdkCallAudioRegistrar>.Instance),
            NullLogger<VoiceChannelProvisioner>.Instance);

    private static SipAccount Account(string id, string workspaceKey) =>
        new(
            id,
            workspaceKey,
            $"Account {id}",
            new SipConnection("sip.example.com", 5060, SipTransport.Udp, SipAccountMode.Trunk, IpAuthentication.Instance, registrationExpirySeconds: null),
            maxConcurrentCalls: 1,
            enabled: true);
}

/// <summary>A configurable <see cref="IVoiceChannelConnector"/> double keyed by account id.</summary>
internal sealed class FakeVoiceChannelConnector : IVoiceChannelConnector
{
    private readonly Dictionary<string, Func<IVoiceChannel?>> _outcomes = new(StringComparer.Ordinal);

    public FakeVoiceChannelConnector Returns(string accountId, IVoiceChannel? channel)
    {
        _outcomes[accountId] = () => channel;
        return this;
    }

    public FakeVoiceChannelConnector Throws(string accountId)
    {
        _outcomes[accountId] = () => throw new InvalidOperationException("connect failed");
        return this;
    }

    public Task<IVoiceChannel?> ConnectAsync(SipAccount account, CancellationToken cancellationToken = default) =>
        Task.FromResult(_outcomes.TryGetValue(account.Id, out var outcome) ? outcome() : null);
}
