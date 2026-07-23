using System;
using System.Collections.Generic;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// The runtime-capability registry (Runtime-Capability S2): it seeds effective state from a source's
/// grants, flips to satisfied immediately, dampens satisfied→unsatisfied by the grace period, cancels
/// the grace timer on early return, and flips a removed source's capabilities off. Time is driven by a
/// <see cref="FakeTimeProvider"/> so the grace timer is deterministic.
/// </summary>
public sealed class RuntimeCapabilityRegistryTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    [Fact]
    public void Register_SeedsGrants_AsSatisfied_AndRaisesFlip()
    {
        var (registry, flips) = NewRegistry();
        var source = new FakeRuntimeCapabilitySource(new RuntimeCapabilityGrant("comm.voice", "ws-1"));

        registry.Register("comm", source);

        Assert.True(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Equal(new RuntimeCapabilityFlip("comm", "comm.voice", "ws-1", true), Assert.Single(flips));
    }

    [Fact]
    public void Unsatisfied_WithinGrace_StaysSatisfied_NoFlip()
    {
        var (registry, flips) = NewRegistry(out var time);
        var source = Registered(registry, new RuntimeCapabilityGrant("comm.voice", "ws-1"));
        flips.Clear();

        source.Raise("comm.voice", "ws-1", satisfied: false);
        time.Advance(TimeSpan.FromSeconds(29)); // still inside the grace window

        Assert.True(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Empty(flips);
    }

    [Fact]
    public void Unsatisfied_AfterGrace_FlipsToUnsatisfied()
    {
        var (registry, flips) = NewRegistry(out var time);
        var source = Registered(registry, new RuntimeCapabilityGrant("comm.voice", "ws-1"));
        flips.Clear();

        source.Raise("comm.voice", "ws-1", satisfied: false);
        time.Advance(Grace);

        Assert.False(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Equal(new RuntimeCapabilityFlip("comm", "comm.voice", "ws-1", false), Assert.Single(flips));
    }

    [Fact]
    public void Return_BeforeGraceElapses_CancelsTimer_StaysSatisfied()
    {
        var (registry, flips) = NewRegistry(out var time);
        var source = Registered(registry, new RuntimeCapabilityGrant("comm.voice", "ws-1"));
        flips.Clear();

        source.Raise("comm.voice", "ws-1", satisfied: false);
        time.Advance(TimeSpan.FromSeconds(10));
        source.Raise("comm.voice", "ws-1", satisfied: true); // returns before grace elapses
        time.Advance(Grace); // the (cancelled) timer must not fire

        Assert.True(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Empty(flips); // no visible transition (was and stays satisfied)
    }

    [Fact]
    public void Return_AfterFlip_IsImmediate()
    {
        var (registry, flips) = NewRegistry(out var time);
        var source = Registered(registry, new RuntimeCapabilityGrant("comm.voice", "ws-1"));
        source.Raise("comm.voice", "ws-1", satisfied: false);
        time.Advance(Grace); // now effectively unsatisfied
        flips.Clear();

        source.Raise("comm.voice", "ws-1", satisfied: true);

        Assert.True(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Equal(new RuntimeCapabilityFlip("comm", "comm.voice", "ws-1", true), Assert.Single(flips));
    }

    [Fact]
    public void RepeatedUnsatisfied_DoesNotExtendGrace_FirstReportWins()
    {
        var (registry, flips) = NewRegistry(out var time);
        var source = Registered(registry, new RuntimeCapabilityGrant("comm.voice", "ws-1"));
        flips.Clear();

        source.Raise("comm.voice", "ws-1", satisfied: false); // grace starts here
        time.Advance(TimeSpan.FromSeconds(20));
        source.Raise("comm.voice", "ws-1", satisfied: false); // must NOT reset the timer
        time.Advance(TimeSpan.FromSeconds(10)); // 30s since the first report → flips now

        Assert.False(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Single(flips);
    }

    [Fact]
    public void Unregister_FlipsSatisfiedCapabilitiesOff_Immediately()
    {
        var (registry, flips) = NewRegistry();
        Registered(registry, new RuntimeCapabilityGrant("comm.voice", "ws-1"));
        flips.Clear();

        registry.Unregister("comm");

        Assert.False(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Equal(new RuntimeCapabilityFlip("comm", "comm.voice", "ws-1", false), Assert.Single(flips));
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        var (registry, _) = NewRegistry();
        registry.Register("comm", new FakeRuntimeCapabilitySource());

        Assert.Throws<InvalidOperationException>(() => registry.Register("comm", new FakeRuntimeCapabilitySource()));
    }

    [Fact]
    public void Grants_AreScopeIsolated()
    {
        var (registry, _) = NewRegistry();
        Registered(registry, new RuntimeCapabilityGrant("comm.voice", "ws-1"));

        Assert.True(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.False(registry.IsSatisfied("comm", "comm.voice", "ws-2")); // other workspace
        Assert.False(registry.IsSatisfied("comm", "comm.voice", workspaceKey: null)); // global
    }

    [Fact]
    public void IsSatisfied_IsCaseInsensitive_ForPluginAndCapability()
    {
        var (registry, _) = NewRegistry();
        Registered(registry, new RuntimeCapabilityGrant("Comm.Voice", "WS-1"));

        Assert.True(registry.IsSatisfied("COMM", "comm.voice", "ws-1"));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource_AndClearsState()
    {
        var (registry, flips) = NewRegistry();
        var source = Registered(registry, new RuntimeCapabilityGrant("comm.voice", "ws-1"));
        flips.Clear();

        registry.Dispose();
        source.Raise("comm.voice", "ws-1", satisfied: false); // ignored after dispose

        Assert.False(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Empty(flips);
    }

    private static (RuntimeCapabilityRegistry Registry, List<RuntimeCapabilityFlip> Flips) NewRegistry() =>
        NewRegistry(out _);

    private static (RuntimeCapabilityRegistry Registry, List<RuntimeCapabilityFlip> Flips) NewRegistry(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider();
        var registry = new RuntimeCapabilityRegistry(Grace, time);
        var flips = new List<RuntimeCapabilityFlip>();
        registry.EffectiveChanged += flips.Add;
        return (registry, flips);
    }

    private static FakeRuntimeCapabilitySource Registered(RuntimeCapabilityRegistry registry, params RuntimeCapabilityGrant[] grants)
    {
        var source = new FakeRuntimeCapabilitySource(grants);
        registry.Register("comm", source);
        return source;
    }
}

/// <summary>A controllable <see cref="IRuntimeCapabilitySource"/> double.</summary>
internal sealed class FakeRuntimeCapabilitySource : IRuntimeCapabilitySource
{
    private readonly List<RuntimeCapabilityGrant> _grants;

    public FakeRuntimeCapabilitySource(params RuntimeCapabilityGrant[] initialGrants) => _grants = [.. initialGrants];

    public IReadOnlyCollection<RuntimeCapabilityGrant> CurrentGrants => _grants;

    public event Action<RuntimeCapabilityChanged>? CapabilitiesChanged;

    public void Raise(string capability, string? workspaceKey, bool satisfied) =>
        CapabilitiesChanged?.Invoke(new RuntimeCapabilityChanged(capability, workspaceKey, satisfied));
}
