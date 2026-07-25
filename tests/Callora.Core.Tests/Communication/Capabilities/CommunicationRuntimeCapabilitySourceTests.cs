using System;
using System.Collections.Generic;
using System.Linq;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Capabilities;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Xunit;

namespace Callora.Core.Tests.Communication.Capabilities;

/// <summary>
/// The Communication runtime-capability source (Runtime-Capability S4b): <c>communication.voice</c> is
/// granted in a workspace exactly while it has a registered voice channel with <c>Health==Up</c>. It
/// reacts to channel registration/removal and health transitions and emits changes per workspace.
/// </summary>
public sealed class CommunicationRuntimeCapabilitySourceTests
{
    private const string Voice = CommunicationCapabilities.Voice;

    [Fact]
    public void HealthyVoiceChannel_Registered_GrantsVoice_AndEmits()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var changes = Capture(source);

        registry.Register("ws-1", VoiceChannel("ch-1", ChannelHealth.Up));

        Assert.Equal(new RuntimeCapabilityChanged(Voice, "ws-1", true), Assert.Single(changes));
        Assert.Equal([new RuntimeCapabilityGrant(Voice, "ws-1")], source.CurrentGrants);
    }

    [Fact]
    public void VoiceChannelHealthDown_EmitsUnsatisfied()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var channel = VoiceChannel("ch-1", ChannelHealth.Up);
        registry.Register("ws-1", channel);
        var changes = Capture(source);

        channel.Health = ChannelHealth.Down;
        channel.RaiseHealthChanged(ChannelHealth.Down);

        Assert.Equal(new RuntimeCapabilityChanged(Voice, "ws-1", false), Assert.Single(changes));
        Assert.Empty(source.CurrentGrants);
    }

    [Fact]
    public void SecondHealthyChannel_KeepsVoiceSatisfied_WhenFirstGoesDown()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var first = VoiceChannel("ch-1", ChannelHealth.Up);
        registry.Register("ws-1", first);
        registry.Register("ws-1", VoiceChannel("ch-2", ChannelHealth.Up));
        var changes = Capture(source);

        first.Health = ChannelHealth.Down;
        first.RaiseHealthChanged(ChannelHealth.Down);

        Assert.Empty(changes); // ch-2 still healthy → voice stays available, no flip
        Assert.Single(source.CurrentGrants);
    }

    [Fact]
    public void UnregisteringLastHealthyChannel_EmitsUnsatisfied()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var handle = registry.Register("ws-1", VoiceChannel("ch-1", ChannelHealth.Up));
        var changes = Capture(source);

        handle.Dispose(); // deregisters the channel

        Assert.Equal(new RuntimeCapabilityChanged(Voice, "ws-1", false), Assert.Single(changes));
        Assert.Empty(source.CurrentGrants);
    }

    [Fact]
    public void NonVoiceChannel_IsIgnored()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var changes = Capture(source);

        registry.Register("ws-1", new FakeVoiceChannel { ChannelId = "ch-x", Capabilities = [], Health = ChannelHealth.Up });

        Assert.Empty(changes);
        Assert.Empty(source.CurrentGrants);
    }

    [Fact]
    public void UnhealthyVoiceChannel_GrantsNothing()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var changes = Capture(source);

        registry.Register("ws-1", VoiceChannel("ch-1", ChannelHealth.Down));

        Assert.Empty(changes);
        Assert.Empty(source.CurrentGrants);
    }

    [Fact]
    public void Seeding_ReflectsPreexistingRegistrations_WithoutEmitting()
    {
        var registry = new CommunicationChannelRegistry();
        registry.Register("ws-1", VoiceChannel("ch-1", ChannelHealth.Up)); // registered before the source exists

        using var source = new CommunicationRuntimeCapabilitySource(registry);
        var changes = Capture(source);

        Assert.Equal([new RuntimeCapabilityGrant(Voice, "ws-1")], source.CurrentGrants);
        Assert.Empty(changes); // seeding does not replay events
    }

    [Fact]
    public void Grants_AreWorkspaceIsolated()
    {
        var registry = new CommunicationChannelRegistry();
        using var source = new CommunicationRuntimeCapabilitySource(registry);
        registry.Register("ws-1", VoiceChannel("ch-1", ChannelHealth.Up));

        Assert.Equal([new RuntimeCapabilityGrant(Voice, "ws-1")], source.CurrentGrants); // only ws-1, not ws-2
    }

    [Fact]
    public void Dispose_StopsReactingToRegistryAndHealthChanges()
    {
        var registry = new CommunicationChannelRegistry();
        var source = new CommunicationRuntimeCapabilitySource(registry);
        var channel = VoiceChannel("ch-1", ChannelHealth.Up);
        registry.Register("ws-1", channel);
        var changes = Capture(source);

        source.Dispose();
        registry.Register("ws-2", VoiceChannel("ch-2", ChannelHealth.Up));
        channel.Health = ChannelHealth.Down;
        channel.RaiseHealthChanged(ChannelHealth.Down);

        Assert.Empty(changes);
    }

    private static List<RuntimeCapabilityChanged> Capture(CommunicationRuntimeCapabilitySource source)
    {
        var changes = new List<RuntimeCapabilityChanged>();
        source.CapabilitiesChanged += changes.Add;
        return changes;
    }

    private static FakeVoiceChannel VoiceChannel(string channelId, ChannelHealth health) =>
        new() { ChannelId = channelId, Health = health };
}
