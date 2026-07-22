using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Xunit;

namespace Callora.Core.Tests.Communication.Channels;

/// <summary>
/// The host channel registry (F7): workspace-isolated registrations, a duplicate-channel guard,
/// registered/unregistered events, dispose-once handles and a stop/unload clear.
/// </summary>
public sealed class CommunicationChannelRegistryTests
{
    [Fact]
    public void Register_ThenGetChannels_ReturnsIt_AndFiresRegistered()
    {
        var registry = new CommunicationChannelRegistry();
        (string Workspace, string ChannelId)? registered = null;
        registry.ChannelRegistered += (ws, ch) => registered = (ws, ch.ChannelId);
        var channel = new FakeChannel("sip-a", CommunicationCapabilities.Voice);

        registry.Register("ws-a", channel);

        Assert.Equal(channel, Assert.Single(registry.GetChannels("ws-a")));
        Assert.Equal(("ws-a", "sip-a"), registered);
    }

    [Fact]
    public void Register_DuplicateChannelId_SameWorkspace_Throws()
    {
        var registry = new CommunicationChannelRegistry();
        registry.Register("ws-a", new FakeChannel("sip-a"));

        Assert.Throws<InvalidOperationException>(() => registry.Register("ws-a", new FakeChannel("sip-a")));
    }

    [Fact]
    public void Register_SameChannelId_DifferentWorkspaces_AreIsolated()
    {
        var registry = new CommunicationChannelRegistry();
        registry.Register("ws-a", new FakeChannel("sip-a"));
        registry.Register("ws-b", new FakeChannel("sip-a"));

        Assert.Single(registry.GetChannels("ws-a"));
        Assert.Single(registry.GetChannels("ws-b"));
        Assert.Empty(registry.GetChannels("ws-other"));
    }

    [Fact]
    public void GetChannelsByCapability_FiltersWithinWorkspace()
    {
        var registry = new CommunicationChannelRegistry();
        registry.Register("ws-a", new FakeChannel("voice", CommunicationCapabilities.Voice));
        registry.Register("ws-a", new FakeChannel("data"));

        var voice = registry.GetChannelsByCapability("ws-a", CommunicationCapabilities.Voice);

        Assert.Equal("voice", Assert.Single(voice).ChannelId);
    }

    [Fact]
    public void TryGetChannel_FoundAndNotFound()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new FakeChannel("sip-a");
        registry.Register("ws-a", channel);

        Assert.True(registry.TryGetChannel("ws-a", "sip-a", out var found));
        Assert.Equal(channel, found);
        Assert.False(registry.TryGetChannel("ws-a", "missing", out var missing));
        Assert.Null(missing);
        Assert.False(registry.TryGetChannel("ws-other", "sip-a", out _));
    }

    [Fact]
    public void DisposeHandle_RemovesRegistration_AndFiresUnregistered()
    {
        var registry = new CommunicationChannelRegistry();
        var unregistered = new List<string>();
        registry.ChannelUnregistered += (_, ch) => unregistered.Add(ch.ChannelId);
        var handle = registry.Register("ws-a", new FakeChannel("sip-a"));

        handle.Dispose();

        Assert.Empty(registry.GetChannels("ws-a"));
        Assert.Equal(["sip-a"], unregistered);
    }

    [Fact]
    public void DisposeHandle_Twice_UnregistersOnce()
    {
        var registry = new CommunicationChannelRegistry();
        var unregisterCount = 0;
        registry.ChannelUnregistered += (_, _) => unregisterCount++;
        var handle = registry.Register("ws-a", new FakeChannel("sip-a"));

        handle.Dispose();
        handle.Dispose();

        Assert.Equal(1, unregisterCount);
    }

    [Fact]
    public void DisposeHandle_AfterClear_DoesNotFireUnregisteredAgain()
    {
        var registry = new CommunicationChannelRegistry();
        var unregisterCount = 0;
        registry.ChannelUnregistered += (_, _) => unregisterCount++;
        var handle = registry.Register("ws-a", new FakeChannel("sip-a"));

        registry.Clear();  // fires Unregistered once
        handle.Dispose();  // entry already gone → no second event

        Assert.Equal(1, unregisterCount);
    }

    [Fact]
    public void GetAllRegistrations_SnapshotsAcrossWorkspaces()
    {
        var registry = new CommunicationChannelRegistry();
        registry.Register("ws-a", new FakeChannel("sip-a"));
        registry.Register("ws-b", new FakeChannel("sip-b"));

        var all = registry.GetAllRegistrations();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, r => r.WorkspaceKey == "ws-a" && r.Channel.ChannelId == "sip-a");
        Assert.Contains(all, r => r.WorkspaceKey == "ws-b" && r.Channel.ChannelId == "sip-b");
    }

    [Fact]
    public void Clear_RemovesAll_AndFiresUnregisteredPerChannel()
    {
        var registry = new CommunicationChannelRegistry();
        var unregistered = new List<string>();
        registry.ChannelUnregistered += (_, ch) => unregistered.Add(ch.ChannelId);
        registry.Register("ws-a", new FakeChannel("sip-a"));
        registry.Register("ws-b", new FakeChannel("sip-b"));

        registry.Clear();

        Assert.Empty(registry.GetAllRegistrations());
        Assert.Equal(2, unregistered.Count);
    }
}

internal sealed class FakeChannel(string channelId, params string[] capabilities) : ICommunicationChannel
{
    public string ChannelId { get; } = channelId;

    public string DisplayName => $"Channel {ChannelId}";

    public string PluginId => "communication";

    public IReadOnlyCollection<string> Capabilities { get; } = capabilities;

    public ChannelHealth Health => ChannelHealth.Up;

#pragma warning disable CS0067 // Test double: the registry never raises this event.
    public event EventHandler<IncomingCallEventArgs>? IncomingCall;
#pragma warning restore CS0067

    public Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Test double.");
}
