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

    private readonly bool _conferenceBridgingAvailable;

    /// <summary>
    /// Capabilities this source derives instead of taking them from a channel — named here so that a
    /// declared capability is provably reachable either way, and a derivation cannot be added without
    /// being visible where declarations are checked.
    /// </summary>
    public static IReadOnlyCollection<string> DerivedCapabilities { get; } =
        [CommunicationCapabilities.ConferenceTelephony];

    /// <summary>
    /// Wires the source to the channel registry and seeds from its current registrations.
    /// </summary>
    /// <param name="registry">The channel registry whose health the grants are derived from.</param>
    /// <param name="conferenceBridgingAvailable">
    /// Whether this composition offers the attachment that puts a call into a conference. It is a
    /// property of the deployment, not of a workspace, so it gates
    /// <see cref="CommunicationCapabilities.ConferenceTelephony"/> rather than being derived per
    /// workspace: without the attachment that capability would be a promise nothing can keep.
    /// </param>
    public CommunicationRuntimeCapabilitySource(
        ICommunicationChannelRegistry registry,
        bool conferenceBridgingAvailable = false)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _conferenceBridgingAvailable = conferenceBridgingAvailable;

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

    private HashSet<RuntimeCapabilityGrant> GetAvailableGrants(string workspaceKey)
    {
        var declared = _registry.GetChannels(workspaceKey)
            .Where(channel => channel.Health == ChannelHealth.Up)
            .SelectMany(channel => channel.Capabilities)
            .Where(capability => !string.IsNullOrWhiteSpace(capability))
            .Select(capability => capability.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var grants = declared
            .Select(capability => new RuntimeCapabilityGrant(capability, workspaceKey))
            .ToHashSet(RuntimeCapabilityGrantComparer.Instance);

        // Telephony into a conference is the one capability no channel can declare on its own: it holds
        // only where a call can be made and a room exists to put it into, and where this composition
        // has the attachment that joins the two. Deriving it here keeps that conjunction in one place
        // instead of asking a channel to know about the others.
        if (_conferenceBridgingAvailable &&
            declared.Contains(CommunicationCapabilities.Voice) &&
            declared.Contains(CommunicationCapabilities.Video))
        {
            grants.Add(new RuntimeCapabilityGrant(CommunicationCapabilities.ConferenceTelephony, workspaceKey));
        }

        return grants;
    }

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
