using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication;
using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Tests.Support;
using Callora.Plugins.Voip.Application.Accounts;
using Callora.Plugins.Voip.Application.Channels;
using Xunit;

namespace Callora.Host.Backend.Tests.Plugins.Voip;

public sealed class SipChannelManagerTests
{
    [Fact]
    public async Task SynchronizeWorkspace_RegistersOneChannelPerActiveAccount()
    {
        var (manager, registry, store) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await store.CreateAsync("workspace-a", NewRequest("bob", isActive: false));

        await manager.SynchronizeWorkspaceAsync("workspace-a");

        var channels = registry.GetChannels("workspace-a");
        var channel = Assert.Single(channels);
        Assert.Contains(CommunicationCapabilities.Voice, channel.Capabilities);
        Assert.Equal("voip", channel.PluginId);
    }

    [Fact]
    public async Task SynchronizeWorkspace_RemovesChannelsForDeletedAccounts()
    {
        var (manager, registry, store) = CreateManager();
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
        var (manager, registry, store) = CreateManager();
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
        var store = new DataStoreSipAccountStore(new InMemoryPluginDataStore());
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
        var (manager, registry, store) = CreateManager();
        await store.CreateAsync("workspace-a", NewRequest("alice", isActive: true));
        await manager.SynchronizeWorkspaceAsync("workspace-a");

        await manager.DisposeAsync();

        Assert.Empty(registry.GetChannels("workspace-a"));
    }

    private static (SipChannelManager Manager, CommunicationChannelRegistry Registry, DataStoreSipAccountStore Store) CreateManager()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new DataStoreSipAccountStore(new InMemoryPluginDataStore());
        var manager = new SipChannelManager(registry, new FakeVoiceEngine(), store);
        return (manager, registry, store);
    }

    private static UpsertSipAccountRequest NewRequest(string username, bool isActive) =>
        new(
            Username: username,
            Domain: "voice.example.org",
            DisplayName: $"{username} Display",
            Secret: "secret",
            IsActive: isActive);
}
