using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Infrastructure.Capabilities;

/// <summary>
/// Derives the plugin's runtime <c>communication.voice</c> grants from the channel registry: voice is
/// provided in a workspace exactly while that workspace has at least one registered voice channel whose
/// <see cref="ICommunicationChannel.Health"/> is <see cref="ChannelHealth.Up"/>. It tracks channel
/// registration/removal and each voice channel's <see cref="ICommunicationChannel.HealthChanged"/>,
/// emitting <see cref="CapabilitiesChanged"/> when a workspace's voice availability flips. The grace
/// dampening of a loss is applied downstream by the host's runtime-capability registry.
/// </summary>
/// <remarks>Thread-safe. <see cref="CapabilitiesChanged"/> is raised outside the internal lock.</remarks>
public sealed class CommunicationRuntimeCapabilitySource : IRuntimeCapabilitySource, IDisposable
{
    private readonly ICommunicationChannelRegistry _registry;
    private readonly object _gate = new();
    private readonly Dictionary<ICommunicationChannel, (string WorkspaceKey, EventHandler<ChannelHealthChangedEventArgs> Handler)> _tracked =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _satisfiedWorkspaces = new(StringComparer.OrdinalIgnoreCase);

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

            foreach (var workspaceKey in _tracked.Values.Select(t => t.WorkspaceKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
            {
                if (IsVoiceAvailable(workspaceKey))
                {
                    _satisfiedWorkspaces.Add(workspaceKey);
                }
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
                return _satisfiedWorkspaces
                    .Select(workspaceKey => new RuntimeCapabilityGrant(CommunicationCapabilities.Voice, workspaceKey))
                    .ToArray();
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

    // Subscribes to a voice channel's health changes; returns false for non-voice or already-tracked channels.
    private bool Track(string workspaceKey, ICommunicationChannel channel)
    {
        // Match the registry's own capability lookup exactly (GetChannelsByCapability uses the default
        // ordinal comparer), so a tracked channel is always one IsVoiceAvailable can also find.
        if (!channel.Capabilities.Contains(CommunicationCapabilities.Voice, StringComparer.Ordinal))
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
        RuntimeCapabilityChanged? change = null;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var satisfied = IsVoiceAvailable(workspaceKey);
            var wasSatisfied = _satisfiedWorkspaces.Contains(workspaceKey);
            if (satisfied && !wasSatisfied)
            {
                _satisfiedWorkspaces.Add(workspaceKey);
                change = new RuntimeCapabilityChanged(CommunicationCapabilities.Voice, workspaceKey, Satisfied: true);
            }
            else if (!satisfied && wasSatisfied)
            {
                _satisfiedWorkspaces.Remove(workspaceKey);
                change = new RuntimeCapabilityChanged(CommunicationCapabilities.Voice, workspaceKey, Satisfied: false);
            }
        }

        if (change is not null)
        {
            CapabilitiesChanged?.Invoke(change);
        }
    }

    private bool IsVoiceAvailable(string workspaceKey) =>
        _registry.GetChannelsByCapability(workspaceKey, CommunicationCapabilities.Voice)
            .Any(channel => channel.Health == ChannelHealth.Up);

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
            _satisfiedWorkspaces.Clear();
        }

        foreach (var (channel, handler) in subscriptions)
        {
            channel.HealthChanged -= handler;
        }
    }
}
