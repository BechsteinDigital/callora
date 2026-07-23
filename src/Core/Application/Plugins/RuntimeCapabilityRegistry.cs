using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Tracks the <em>effective</em> runtime-conditional capability state of registered plugins. Each
/// plugin exports an <see cref="IRuntimeCapabilitySource"/>; the registry seeds from its snapshot,
/// follows its <see cref="IRuntimeCapabilitySource.CapabilitiesChanged"/> events, and dampens the
/// satisfied→unsatisfied direction by a grace period so a transient loss does not immediately flip.
/// Return-to-satisfied is applied immediately. <see cref="IsSatisfied"/> is the effective query the
/// capability guard consults; <see cref="EffectiveChanged"/> fires on every effective flip so
/// availability-derived gates can be invalidated.
/// </summary>
/// <remarks>Thread-safe. Events are raised outside the internal lock.</remarks>
public sealed class RuntimeCapabilityRegistry : IDisposable
{
    private readonly TimeSpan _gracePeriod;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    private readonly Dictionary<RuntimeCapabilityKey, RuntimeCapabilityEntry> _effective = [];
    private readonly Dictionary<RuntimeCapabilityKey, ITimer> _pendingTimers = [];
    private readonly Dictionary<string, (IRuntimeCapabilitySource Source, Action<RuntimeCapabilityChanged> Handler)> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    /// <summary>Creates a registry with the given grace period and time source.</summary>
    /// <param name="gracePeriod">
    /// How long an unsatisfied report must persist before the effective state flips to unsatisfied.
    /// <see cref="TimeSpan.Zero"/> flips immediately.
    /// </param>
    /// <param name="timeProvider">Time source for the grace timer (inject a fake in tests).</param>
    public RuntimeCapabilityRegistry(TimeSpan gracePeriod, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gracePeriod, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _gracePeriod = gracePeriod;
        _timeProvider = timeProvider;
    }

    /// <summary>Raised whenever a plugin's effective runtime-capability state flips (after any grace period).</summary>
    public event Action<RuntimeCapabilityFlip>? EffectiveChanged;

    /// <summary>
    /// Registers a plugin's runtime-capability source: subscribes to its changes and seeds the effective
    /// state from its current grants (which flip to satisfied). Throws if the plugin is already registered.
    /// </summary>
    public void Register(string pluginId, IRuntimeCapabilitySource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(source);

        List<RuntimeCapabilityFlip> flips = [];
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_registrations.ContainsKey(pluginId))
            {
                throw new InvalidOperationException($"A runtime capability source for plugin '{pluginId}' is already registered.");
            }

            void Handler(RuntimeCapabilityChanged change) => OnCapabilityChanged(pluginId, change);
            _registrations[pluginId] = (source, Handler);
            source.CapabilitiesChanged += Handler;

            foreach (var grant in source.CurrentGrants)
            {
                Apply(pluginId, grant.Capability, grant.WorkspaceKey, satisfied: true, flips);
            }
        }

        RaiseFlips(flips);
    }

    /// <summary>
    /// Removes a plugin's source: unsubscribes, cancels pending grace timers and flips any of its
    /// still-satisfied capabilities to unsatisfied (a removed source provides nothing).
    /// </summary>
    public void Unregister(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        List<RuntimeCapabilityFlip> flips = [];
        lock (_gate)
        {
            if (!_registrations.Remove(pluginId, out var registration))
            {
                return;
            }

            registration.Source.CapabilitiesChanged -= registration.Handler;

            foreach (var (key, entry) in _effective.Where(kv => string.Equals(kv.Value.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                CancelTimer(key);
                if (entry.Satisfied)
                {
                    flips.Add(new RuntimeCapabilityFlip(entry.PluginId, entry.Capability, entry.WorkspaceKey, Satisfied: false));
                }

                _effective.Remove(key);
            }
        }

        RaiseFlips(flips);
    }

    /// <summary>Returns whether the plugin currently, effectively provides the capability in the scope.</summary>
    public bool IsSatisfied(string pluginId, string capability, string? workspaceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);

        var key = RuntimeCapabilityKey.Create(pluginId, capability, workspaceKey);
        lock (_gate)
        {
            return _effective.TryGetValue(key, out var entry) && entry.Satisfied;
        }
    }

    private void OnCapabilityChanged(string pluginId, RuntimeCapabilityChanged change)
    {
        List<RuntimeCapabilityFlip> flips = [];
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var key = RuntimeCapabilityKey.Create(pluginId, change.Capability, change.WorkspaceKey);
            if (change.Satisfied)
            {
                // Return-to-satisfied is immediate and cancels any pending grace timer.
                CancelTimer(key);
                Apply(pluginId, change.Capability, change.WorkspaceKey, satisfied: true, flips);
            }
            else if (IsCurrentlySatisfied(key))
            {
                if (_gracePeriod <= TimeSpan.Zero)
                {
                    Apply(pluginId, change.Capability, change.WorkspaceKey, satisfied: false, flips);
                }
                else
                {
                    StartGraceTimer(key, pluginId, change.Capability, change.WorkspaceKey);
                }
            }
        }

        RaiseFlips(flips);
    }

    private void StartGraceTimer(RuntimeCapabilityKey key, string pluginId, string capability, string? workspaceKey)
    {
        if (_pendingTimers.ContainsKey(key))
        {
            return; // already counting down; the first unsatisfied report wins.
        }

        ITimer timer = null!;
        timer = _timeProvider.CreateTimer(
            _ => OnGraceElapsed(key, timer, pluginId, capability, workspaceKey),
            state: null,
            dueTime: _gracePeriod,
            period: Timeout.InfiniteTimeSpan);
        _pendingTimers[key] = timer;
    }

    private void OnGraceElapsed(RuntimeCapabilityKey key, ITimer timer, string pluginId, string capability, string? workspaceKey)
    {
        List<RuntimeCapabilityFlip> flips = [];
        lock (_gate)
        {
            // Only flip if this exact timer is still the pending one (not cancelled/superseded).
            if (_pendingTimers.TryGetValue(key, out var current) && ReferenceEquals(current, timer))
            {
                _pendingTimers.Remove(key);
                Apply(pluginId, capability, workspaceKey, satisfied: false, flips);
            }
        }

        timer.Dispose();
        RaiseFlips(flips);
    }

    // Applies an effective state change under the lock, appending a flip when the state actually changed.
    private void Apply(string pluginId, string capability, string? workspaceKey, bool satisfied, List<RuntimeCapabilityFlip> flips)
    {
        var key = RuntimeCapabilityKey.Create(pluginId, capability, workspaceKey);
        if (IsCurrentlySatisfied(key) == satisfied)
        {
            return;
        }

        _effective[key] = new RuntimeCapabilityEntry(pluginId, capability, workspaceKey, satisfied);
        flips.Add(new RuntimeCapabilityFlip(pluginId, capability, workspaceKey, satisfied));
    }

    private bool IsCurrentlySatisfied(RuntimeCapabilityKey key) =>
        _effective.TryGetValue(key, out var entry) && entry.Satisfied;

    private void CancelTimer(RuntimeCapabilityKey key)
    {
        if (_pendingTimers.Remove(key, out var timer))
        {
            timer.Dispose();
        }
    }

    private void RaiseFlips(List<RuntimeCapabilityFlip> flips)
    {
        if (flips.Count == 0)
        {
            return;
        }

        var handler = EffectiveChanged;
        if (handler is null)
        {
            return;
        }

        foreach (var flip in flips)
        {
            handler(flip);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        List<(IRuntimeCapabilitySource Source, Action<RuntimeCapabilityChanged> Handler)> registrations;
        List<ITimer> timers;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            registrations = [.. _registrations.Values];
            timers = [.. _pendingTimers.Values];
            _registrations.Clear();
            _pendingTimers.Clear();
            _effective.Clear();
        }

        foreach (var registration in registrations)
        {
            registration.Source.CapabilitiesChanged -= registration.Handler;
        }

        foreach (var timer in timers)
        {
            timer.Dispose();
        }
    }
}
