using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Infrastructure.Channels;

/// <summary>
/// The host-provided, in-process implementation of <see cref="ICommunicationChannelRegistry"/>:
/// the place where the SDK bridge registers its voice channels and consumers (dialer, AI agent)
/// resolve them without knowing the providing plugin. Registrations are workspace-isolated — a
/// channel id may repeat across workspaces but not within one — and a registration handle removes
/// exactly its own entry on dispose. Thread-safe: mutations are serialized under a lock, while the
/// registered/unregistered events fire outside it so a handler may safely re-enter the registry.
/// <para>
/// Removal matches the stored <em>instance</em>, not just the channel id (#117). Keying removal on
/// the id alone meant that after a clear-and-re-register cycle, disposing the old handle
/// deregistered the new channel: the provisioner reuses its ids, so the stale handle looked
/// authoritative for a registration it no longer owned.
/// </para>
/// </summary>
public sealed class CommunicationChannelRegistry : ICommunicationChannelRegistry
{
    private readonly object _gate = new();

    // workspaceKey → (channelId → channel). Workspace keys are case-insensitive (tenant axis);
    // channel ids are exact within a workspace.
    private readonly Dictionary<string, Dictionary<string, ICommunicationChannel>> _byWorkspace =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public event Action<string, ICommunicationChannel>? ChannelRegistered;

    /// <inheritdoc />
    public event Action<string, ICommunicationChannel>? ChannelUnregistered;

    /// <inheritdoc />
    public IDisposable Register(string workspaceKey, ICommunicationChannel channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel.ChannelId);

        lock (_gate)
        {
            if (!_byWorkspace.TryGetValue(workspaceKey, out var channels))
            {
                channels = new Dictionary<string, ICommunicationChannel>(StringComparer.Ordinal);
                _byWorkspace[workspaceKey] = channels;
            }

            if (channels.ContainsKey(channel.ChannelId))
            {
                throw new InvalidOperationException(
                    $"A channel '{channel.ChannelId}' is already registered in workspace '{workspaceKey}'.");
            }

            channels[channel.ChannelId] = channel;
        }

        ChannelRegistered?.Invoke(workspaceKey, channel);
        return new ChannelRegistrationHandle(() => Remove(workspaceKey, channel));
    }

    /// <inheritdoc />
    public IReadOnlyList<ICommunicationChannel> GetChannels(string workspaceKey)
    {
        lock (_gate)
        {
            return _byWorkspace.TryGetValue(workspaceKey, out var channels)
                ? channels.Values.ToArray()
                : [];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ICommunicationChannel> GetChannelsByCapability(string workspaceKey, string capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);

        lock (_gate)
        {
            if (!_byWorkspace.TryGetValue(workspaceKey, out var channels))
            {
                return [];
            }

            return channels.Values.Where(channel => channel.Capabilities.Contains(capability)).ToArray();
        }
    }

    /// <inheritdoc />
    public bool TryGetChannel(string workspaceKey, string channelId, out ICommunicationChannel? channel)
    {
        lock (_gate)
        {
            if (_byWorkspace.TryGetValue(workspaceKey, out var channels) &&
                channels.TryGetValue(channelId, out var found))
            {
                channel = found;
                return true;
            }
        }

        channel = null;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<(string WorkspaceKey, ICommunicationChannel Channel)> GetAllRegistrations()
    {
        lock (_gate)
        {
            return _byWorkspace
                .SelectMany(entry => entry.Value.Values.Select(channel => (entry.Key, channel)))
                .ToArray();
        }
    }

    /// <summary>
    /// Removes every registration — used when the providing plugin stops or its load context is
    /// unloaded, so no dangling channels survive. Fires <see cref="ChannelUnregistered"/> for each.
    /// </summary>
    public void Clear()
    {
        List<(string WorkspaceKey, ICommunicationChannel Channel)> removed;
        lock (_gate)
        {
            removed = _byWorkspace
                .SelectMany(entry => entry.Value.Values.Select(channel => (entry.Key, channel)))
                .ToList();
            _byWorkspace.Clear();
        }

        foreach (var (workspaceKey, channel) in removed)
        {
            ChannelUnregistered?.Invoke(workspaceKey, channel);
        }
    }

    /// <summary>
    /// Removes the registration a handle owns. The stored instance must be the one the handle was
    /// created for, otherwise a stale handle disposed after a re-registration would deregister a
    /// live channel that merely reuses the id (#117).
    /// </summary>
    private void Remove(string workspaceKey, ICommunicationChannel channel)
    {
        var removed = false;
        lock (_gate)
        {
            if (_byWorkspace.TryGetValue(workspaceKey, out var channels) &&
                channels.TryGetValue(channel.ChannelId, out var stored) &&
                ReferenceEquals(stored, channel))
            {
                channels.Remove(channel.ChannelId);
                removed = true;

                if (channels.Count == 0)
                {
                    _byWorkspace.Remove(workspaceKey);
                }
            }
        }

        if (removed)
        {
            ChannelUnregistered?.Invoke(workspaceKey, channel);
        }
    }
}
