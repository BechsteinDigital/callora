using System;
using System.Collections.Generic;
using System.Linq;
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
/// The single provisioning path from a persisted account to a live channel (#110): each account is
/// connected through the connector seam, wrapped in an audio-registering decorator and registered
/// under its workspace; a connect failure skips only that account, and teardown deregisters and
/// disposes everything. Reconciliation is idempotent, reconnects on configuration change and
/// deprovisions on disable/delete.
/// </summary>
public sealed class SipAccountRuntimeReconcilerTests
{
    [Fact]
    public async Task ProvisionAsync_ConnectsAndRegistersAllAccounts()
    {
        var registry = new CommunicationChannelRegistry();
        var connector = new FakeVoiceChannelConnector()
            .Returns("a1", new FakeVoiceChannel { ChannelId = "a1" })
            .Returns("a2", new FakeVoiceChannel { ChannelId = "a2" });
        var reconciler = NewReconciler(connector, registry);

        var summary = await reconciler.ApplyAllAsync([Account("a1", "w1"), Account("a2", "w1")]);

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
        var reconciler = NewReconciler(connector, registry);

        await reconciler.ApplyAllAsync([Account("a1", "wA"), Account("a2", "wB")]);

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
        var reconciler = NewReconciler(connector, registry);

        var summary = await reconciler.ApplyAllAsync([Account("a1", "w1"), Account("a2", "w1")]);

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
        var reconciler = NewReconciler(connector, registry);

        var summary = await reconciler.ApplyAllAsync([Account("a1", "w1"), Account("a2", "w1")]);

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
        var reconciler = NewReconciler(connector, registry);
        await reconciler.ApplyAllAsync([Account("a1", "w1"), Account("a2", "w1")]);

        reconciler.Teardown();

        Assert.Empty(registry.GetChannels("w1"));
        Assert.True(innerA.Disposed);
        Assert.True(innerB.Disposed);
    }

    [Fact]
    public async Task ApplyAsync_IsIdempotent_ForAnUnchangedAccount()
    {
        var registry = new CommunicationChannelRegistry();
        var connector = new FakeVoiceChannelConnector().Returns("a1", new FakeVoiceChannel { ChannelId = "a1" });
        var reconciler = NewReconciler(connector, registry);
        var account = Account("a1", "w1");

        await reconciler.ApplyAsync(account);
        var second = await reconciler.ApplyAsync(account);

        Assert.Equal(SipRuntimeState.Connected, second.State);
        Assert.Single(registry.GetChannels("w1"));
        // The second call must not have reconnected — one connect, one registration.
        Assert.Equal(1, connector.ConnectCount("a1"));
    }

    [Fact]
    public async Task ApplyAsync_ReconnectsWhenTheConnectionChanges()
    {
        var registry = new CommunicationChannelRegistry();
        var connector = new FakeVoiceChannelConnector()
            .Returns("a1", new FakeVoiceChannel { ChannelId = "a1" });
        var reconciler = NewReconciler(connector, registry);
        var account = Account("a1", "w1");
        await reconciler.ApplyAsync(account);

        account.Reconfigure(
            "Account a1",
            new SipConnection(
                "other.example.com",
                5060,
                SipTransport.Udp,
                SipAccountMode.Register,
                new DigestAuthentication("user-a1", null, "secret-ref"),
                registrationExpirySeconds: 300),
            maxConcurrentCalls: 1);
        var result = await reconciler.ApplyAsync(account);

        Assert.Equal(SipRuntimeState.Connected, result.State);
        Assert.Equal(2, connector.ConnectCount("a1"));
        Assert.Single(registry.GetChannels("w1"));
    }

    [Fact]
    public async Task ApplyAsync_DisabledAccount_IsDeprovisioned()
    {
        var registry = new CommunicationChannelRegistry();
        var inner = new FakeVoiceChannel { ChannelId = "a1" };
        var reconciler = NewReconciler(new FakeVoiceChannelConnector().Returns("a1", inner), registry);
        var account = Account("a1", "w1");
        await reconciler.ApplyAsync(account);

        account.Disable();
        var result = await reconciler.ApplyAsync(account);

        Assert.Equal(SipRuntimeState.Removed, result.State);
        Assert.Empty(registry.GetChannels("w1"));
        Assert.True(inner.Disposed);
    }

    [Fact]
    public async Task ApplyAsync_ReportsFailure_WhenTheRuntimeRejectsTheAccount()
    {
        var registry = new CommunicationChannelRegistry();
        var reconciler = NewReconciler(new FakeVoiceChannelConnector().Returns("a1", channel: null), registry);

        var result = await reconciler.ApplyAsync(Account("a1", "w1"));

        Assert.Equal(SipRuntimeState.Failed, result.State);
        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Empty(registry.GetChannels("w1"));
    }

    [Fact]
    public async Task RemoveAsync_IsIdempotent()
    {
        var registry = new CommunicationChannelRegistry();
        var reconciler = NewReconciler(
            new FakeVoiceChannelConnector().Returns("a1", new FakeVoiceChannel { ChannelId = "a1" }),
            registry);
        await reconciler.ApplyAsync(Account("a1", "w1"));

        Assert.Equal(SipRuntimeState.Removed, (await reconciler.RemoveAsync("w1", "a1")).State);
        Assert.Equal(SipRuntimeState.Removed, (await reconciler.RemoveAsync("w1", "a1")).State);
        Assert.Empty(registry.GetChannels("w1"));
    }

    [Fact]
    public async Task ConcurrentApplies_ForTheSameAccount_ProduceOneRegistration()
    {
        var registry = new CommunicationChannelRegistry();
        var connector = new FakeVoiceChannelConnector().Returns("a1", new FakeVoiceChannel { ChannelId = "a1" });
        var reconciler = NewReconciler(connector, registry);
        var account = Account("a1", "w1");

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => reconciler.ApplyAsync(account)));

        Assert.Single(registry.GetChannels("w1"));
        Assert.Equal(1, connector.ConnectCount("a1"));
    }

    private static SipAccountRuntimeReconciler NewReconciler(
        IVoiceChannelConnector connector,
        CommunicationChannelRegistry registry) =>
        new(
            connector,
            registry,
            new SdkCallAudioRegistrar(new SdkCallAudioStreamProvider(), NullLogger<SdkCallAudioRegistrar>.Instance),
            NullLogger<SipAccountRuntimeReconciler>.Instance);

    // Digest is the only method the provider connects (#111); an IP-authenticated account
    // would be refused by the reconciler before it ever reached the connector, which would
    // make these provisioning assertions vacuous.
    private static SipAccount Account(string id, string workspaceKey) =>
        new(
            id,
            workspaceKey,
            $"Account {id}",
            new SipConnection(
                "sip.example.com",
                5060,
                SipTransport.Udp,
                SipAccountMode.Register,
                new DigestAuthentication($"user-{id}", null, "secret-ref"),
                registrationExpirySeconds: 300),
            maxConcurrentCalls: 1,
            enabled: true);
}

/// <summary>A configurable <see cref="IVoiceChannelConnector"/> double keyed by account id.</summary>
internal sealed class FakeVoiceChannelConnector : IVoiceChannelConnector
{
    private readonly Dictionary<string, Func<IVoiceChannel?>> _outcomes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _connects = new(StringComparer.Ordinal);
    private readonly Lock _sync = new();

    /// <summary>How often the account was connected — proves idempotence and reconnects.</summary>
    public int ConnectCount(string accountId)
    {
        lock (_sync)
        {
            return _connects.TryGetValue(accountId, out var count) ? count : 0;
        }
    }

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

    public Task<IVoiceChannel?> ConnectAsync(SipAccount account, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _connects[account.Id] = ConnectCountUnlocked(account.Id) + 1;
        }

        return Task.FromResult(_outcomes.TryGetValue(account.Id, out var outcome) ? outcome() : null);
    }

    private int ConnectCountUnlocked(string accountId) =>
        _connects.TryGetValue(accountId, out var count) ? count : 0;
}
