using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Support;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Channels;
using Xunit;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Core.Tests.Plugins.Voip;

public sealed class SipChannelManagerTests
{
    [Fact]
    public async Task SynchronizeWorkspace_RegistersOneChannelPerActiveAccount()
    {
        var (manager, registry, store, _) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await store.CreateAsync("workspace-a", NewRequest("bob", isActive: false));

        await manager.SynchronizeWorkspaceAsync("workspace-a");

        var channels = registry.GetChannels("workspace-a");
        var channel = Assert.Single(channels);
        Assert.Contains(CommunicationCapabilities.Voice, channel.Capabilities);
        Assert.Equal("communication", channel.PluginId);
    }

    [Fact]
    public async Task SynchronizeWorkspace_RemovesChannelsForDeletedAccounts()
    {
        var (manager, registry, store, _) = CreateManager();
        var account = await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await manager.SynchronizeWorkspaceAsync("workspace-a");
        Assert.Single(registry.GetChannels("workspace-a"));

        await store.DeleteAsync("workspace-a", account.SipAccountId);
        await manager.SynchronizeWorkspaceAsync("workspace-a");

        Assert.Empty(registry.GetChannels("workspace-a"));
    }

    [Fact]
    public async Task SynchronizeAll_RegistersChannelsForAllWorkspaces()
    {
        var (manager, registry, store, _) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await store.CreateAsync("workspace-b", NewRequest("bob", isActive: true));

        await manager.SynchronizeAllAsync();

        Assert.Single(registry.GetChannels("workspace-a"));
        Assert.Single(registry.GetChannels("workspace-b"));
    }

    [Fact]
    public async Task RegisteredChannel_PlacesCallsThroughEngine()
    {
        var engine = new FakeVoiceEngine();
        var registry = new CommunicationChannelRegistry();
        var store = new DataStoreSipAccountStore(new InMemoryPluginDataStore(), new FakePluginDataProtector());
        var manager = new SipChannelManager(registry, engine, store);
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await manager.SynchronizeWorkspaceAsync("workspace-a");
        var channel = Assert.Single(registry.GetChannels("workspace-a"));

        var call = await channel.PlaceCallAsync(new CallTarget("+49301234567"));

        var placed = Assert.Single(engine.PlacedCalls);
        Assert.Equal("alice", placed.Account.Username);
        Assert.Equal("+49301234567", placed.Target.Value);
        Assert.Equal(CallState.Connecting, call.State);
    }

    [Fact]
    public async Task DisposeAsync_RemovesAllRegistrations()
    {
        var (manager, registry, store, _) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await manager.SynchronizeWorkspaceAsync("workspace-a");

        await manager.DisposeAsync();

        Assert.Empty(registry.GetChannels("workspace-a"));
    }

    [Fact]
    public async Task SynchronizeWorkspace_SubscribesInboundCallsPerActiveAccount()
    {
        var (manager, _, store, engine) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await store.CreateAsync("workspace-a", NewRequest("bob", isActive: false));

        await manager.SynchronizeWorkspaceAsync("workspace-a");

        var subscription = Assert.Single(engine.IncomingCallSubscriptions);
        Assert.Equal("alice", subscription.Account.Username);
    }

    [Fact]
    public async Task IncomingEngineCall_RaisesChannelEventWithRingingInboundCall()
    {
        var (manager, registry, store, engine) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await manager.SynchronizeWorkspaceAsync("workspace-a");
        var channel = Assert.Single(registry.GetChannels("workspace-a"));
        var receivedCalls = new List<ICall>();
        channel.IncomingCall += (_, args) => receivedCalls.Add(args.Call);

        var engineCall = new FakeEngineCall(SdkCallState.Ringing)
        {
            Direction = CalloraVoipSdk.Core.Domain.Calls.CallDirection.Inbound,
            RemoteParty = "sip:caller@voice.example.org"
        };
        Assert.Single(engine.IncomingCallSubscriptions).RaiseIncomingCall(engineCall);

        var call = Assert.Single(receivedCalls);
        Assert.Equal(CallState.Ringing, call.State);
        Assert.Equal(CallDirection.Inbound, call.Direction);
        Assert.Equal("sip:caller@voice.example.org", call.Target.Value);
    }

    [Fact]
    public async Task Resynchronize_DisposesPreviousInboundSubscriptions()
    {
        var (manager, _, store, engine) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await manager.SynchronizeWorkspaceAsync("workspace-a");
        var firstSubscription = Assert.Single(engine.IncomingCallSubscriptions);

        await manager.SynchronizeWorkspaceAsync("workspace-a");

        Assert.True(firstSubscription.IsDisposed);
        Assert.Equal(2, engine.IncomingCallSubscriptions.Count);
        Assert.False(engine.IncomingCallSubscriptions[1].IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_DisposesInboundSubscriptions()
    {
        var (manager, _, store, engine) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await manager.SynchronizeWorkspaceAsync("workspace-a");

        await manager.DisposeAsync();

        Assert.True(Assert.Single(engine.IncomingCallSubscriptions).IsDisposed);
    }

    [Fact]
    public async Task FailedInboundSubscription_KeepsChannelRegisteredForOutbound()
    {
        var (manager, registry, store, engine) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        engine.NextSubscriptionError = new InvalidOperationException("registrar unreachable");

        await manager.SynchronizeWorkspaceAsync("workspace-a");

        Assert.Single(registry.GetChannels("workspace-a"));
        Assert.Empty(engine.IncomingCallSubscriptions);
    }

    private static (SipChannelManager Manager, CommunicationChannelRegistry Registry, DataStoreSipAccountStore Store, FakeVoiceEngine Engine) CreateManager()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new DataStoreSipAccountStore(new InMemoryPluginDataStore(), new FakePluginDataProtector());
        var engine = new FakeVoiceEngine();
        var manager = new SipChannelManager(registry, engine, store);
        return (manager, registry, store, engine);
    }

    private static UpsertSipAccountRequest NewRequest(string username, bool isActive) =>
        new(
            Username: username,
            Domain: "voice.example.org",
            DisplayName: $"{username} Display",
            Secret: "secret",
            IsActive: isActive);
}
