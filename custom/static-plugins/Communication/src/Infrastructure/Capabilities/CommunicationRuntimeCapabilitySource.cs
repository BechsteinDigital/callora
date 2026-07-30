using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Infrastructure.Capabilities;

/// <summary>
/// Derives runtime communication grants from the SDK-neutral channel registry. A capability is
/// provided in a workspace exactly while that workspace has at least one registered channel declaring
/// it with <see cref="ICommunicationChannel.Health"/> equal to <see cref="ChannelHealth.Up"/>.
/// Voice, video, WebRTC and future channel capabilities follow the same adapter path.
/// </summary>
/// <remarks>Thread-safe. <see cref="CapabilitiesChanged"/> is raised outside the internal lock.</remarks>
public sealed class CommunicationRuntimeCapabilitySource : IRuntimeCapabilitySource, IDisposable
{
    private readonly ICommunicationChannelRegistry _registry;
    private readonly object _gate = new();
    private readonly Dictionary<ICommunicationChannel, (string WorkspaceKey, EventHandler<ChannelHealthChangedEventArgs> Handler)> _tracked =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<RuntimeCapabilityGrant> _grants = new(RuntimeCapabilityGrantComparer.Instance);

    private bool _disposed;

    /// <summary>Wires the source to the channel registry and seeds from its current registrations.</summary>
    public CommunicationRuntimeCapabilitySource(ICommunicationChannelRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;

        _registry.ChannelRegistered += OnChannelRegistered;
        _registry.ChannelUnregistered += OnChannelUnregistered;

        lock (_gate)
        {
            foreach (var (workspaceKey, channel) in _registry.GetAllRegistrations())
            {
                Track(workspaceKey, channel);
            }

            foreach (var workspaceKey in _tracked.Values
                         .Select(t => t.WorkspaceKey)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToArray())
            {
                _grants.UnionWith(GetAvailableGrants(workspaceKey));
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<RuntimeCapabilityGrant> CurrentGrants
    {
        get
        {
            lock (_gate)
            {
                return _grants.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public event Action<RuntimeCapabilityChanged>? CapabilitiesChanged;

    private void OnChannelRegistered(string workspaceKey, ICommunicationChannel channel)
    {
        lock (_gate)
        {
            if (_disposed || !Track(workspaceKey, channel))
            {
                return;
            }
        }

        Reevaluate(workspaceKey);
    }

    private void OnChannelUnregistered(string workspaceKey, ICommunicationChannel channel)
    {
        lock (_gate)
        {
            if (_disposed || !_tracked.Remove(channel, out var tracked))
            {
                return;
            }

            channel.HealthChanged -= tracked.Handler;
        }

        Reevaluate(workspaceKey);
    }

    // Subscribes to a capability-bearing channel's health changes; returns false when it has no declared
    // capability or is already tracked.
    private bool Track(string workspaceKey, ICommunicationChannel channel)
    {
        if (channel.Capabilities.Count == 0)
        {
            return false;
        }

        void Handler(object? sender, ChannelHealthChangedEventArgs e) => Reevaluate(workspaceKey);
        if (!_tracked.TryAdd(channel, (workspaceKey, Handler)))
        {
            return false;
        }

        channel.HealthChanged += Handler;
        return true;
    }

    private void Reevaluate(string workspaceKey)
    {
        List<RuntimeCapabilityChanged> changes;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var current = GetAvailableGrants(workspaceKey);
            var previous = _grants
                .Where(grant => string.Equals(grant.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(RuntimeCapabilityGrantComparer.Instance);

            changes = current
                .Except(previous)
                .Select(grant => new RuntimeCapabilityChanged(grant.Capability, grant.WorkspaceKey, Satisfied: true))
                .Concat(previous
                    .Except(current)
                    .Select(grant => new RuntimeCapabilityChanged(grant.Capability, grant.WorkspaceKey, Satisfied: false)))
                .ToList();

            foreach (var previousGrant in previous)
            {
                _grants.Remove(previousGrant);
            }

            foreach (var currentGrant in current)
            {
                _grants.Add(currentGrant);
            }
        }

        foreach (var change in changes)
        {
            CapabilitiesChanged?.Invoke(change);
        }
    }

    private HashSet<RuntimeCapabilityGrant> GetAvailableGrants(string workspaceKey) =>
        _registry.GetChannels(workspaceKey)
            .Where(channel => channel.Health == ChannelHealth.Up)
            .SelectMany(channel => channel.Capabilities)
            .Where(capability => !string.IsNullOrWhiteSpace(capability))
            .Select(capability => new RuntimeCapabilityGrant(capability.Trim(), workspaceKey))
            .ToHashSet(RuntimeCapabilityGrantComparer.Instance);

    /// <inheritdoc />
    public void Dispose()
    {
        List<(ICommunicationChannel Channel, EventHandler<ChannelHealthChangedEventArgs> Handler)> subscriptions;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _registry.ChannelRegistered -= OnChannelRegistered;
            _registry.ChannelUnregistered -= OnChannelUnregistered;
            subscriptions = [.. _tracked.Select(t => (t.Key, t.Value.Handler))];
            _tracked.Clear();
            _grants.Clear();
        }

        foreach (var (channel, handler) in subscriptions)
        {
            channel.HealthChanged -= handler;
        }
    }
}
