using System.Collections.Concurrent;
using System.Collections.Immutable;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Host.Backend.Application.Communication;

/// <summary>
/// Thread-safe, workspace-scoped in-memory registry of communication channels.
/// Communication plugins register channels on activation; consuming plugins
/// resolve them via <see cref="ICommunicationChannelRegistry"/>.
/// </summary>
public sealed class CommunicationChannelRegistry : ICommunicationChannelRegistry
{
    private readonly ConcurrentDictionary<string, ImmutableArray<ICommunicationChannel>> _channelsByWorkspace =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised after a channel was registered. Host-internal, not part of the plugin contract.</summary>
    public event Action<string, ICommunicationChannel>? ChannelRegistered;

    /// <summary>Raised after a channel was unregistered. Host-internal, not part of the plugin contract.</summary>
    public event Action<string, ICommunicationChannel>? ChannelUnregistered;

    /// <inheritdoc />
    public IDisposable Register(string workspaceKey, ICommunicationChannel channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(channel);
        var normalizedKey = workspaceKey.Trim();

        _channelsByWorkspace.AddOrUpdate(
            normalizedKey,
            _ => ImmutableArray.Create(channel),
            (_, current) =>
            {
                if (current.Any(existing => string.Equals(existing.ChannelId, channel.ChannelId, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        $"Channel '{channel.ChannelId}' is already registered for workspace '{normalizedKey}'.");
                }

                return current.Add(channel);
            });

        ChannelRegistered?.Invoke(normalizedKey, channel);
        return new CommunicationChannelRegistration(this, normalizedKey, channel);
    }

    /// <inheritdoc />
    public IReadOnlyList<ICommunicationChannel> GetChannels(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
            return Array.Empty<ICommunicationChannel>();

        return _channelsByWorkspace.TryGetValue(workspaceKey.Trim(), out var channels)
            ? channels
            : Array.Empty<ICommunicationChannel>();
    }

    /// <inheritdoc />
    public IReadOnlyList<ICommunicationChannel> GetChannelsByCapability(string workspaceKey, string capability)
    {
        if (string.IsNullOrWhiteSpace(capability))
            return Array.Empty<ICommunicationChannel>();

        return GetChannels(workspaceKey)
            .Where(channel => channel.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <inheritdoc />
    public bool TryGetChannel(string workspaceKey, string channelId, out ICommunicationChannel? channel)
    {
        channel = GetChannels(workspaceKey)
            .FirstOrDefault(candidate => string.Equals(candidate.ChannelId, channelId, StringComparison.OrdinalIgnoreCase));
        return channel is not null;
    }

    /// <summary>
    /// Snapshot of all current registrations across workspaces. Host-internal,
    /// used to attach observers to channels registered before they subscribed.
    /// </summary>
    public IReadOnlyList<(string WorkspaceKey, ICommunicationChannel Channel)> GetAllRegistrations()
    {
        return _channelsByWorkspace
            .SelectMany(pair => pair.Value.Select(channel => (pair.Key, channel)))
            .ToArray();
    }

    internal void Unregister(string workspaceKey, ICommunicationChannel channel)
    {
        while (_channelsByWorkspace.TryGetValue(workspaceKey, out var current))
        {
            var updated = current.Remove(channel);
            if (updated == current)
                return;

            if (updated.IsEmpty)
            {
                if (_channelsByWorkspace.TryRemove(
                        new KeyValuePair<string, ImmutableArray<ICommunicationChannel>>(workspaceKey, current)))
                {
                    ChannelUnregistered?.Invoke(workspaceKey, channel);
                    return;
                }
            }
            else if (_channelsByWorkspace.TryUpdate(workspaceKey, updated, current))
            {
                ChannelUnregistered?.Invoke(workspaceKey, channel);
                return;
            }
        }
    }
}
