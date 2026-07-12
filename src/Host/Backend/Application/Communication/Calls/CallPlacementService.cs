using Callora.Contracts.Communication;

namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// Places outbound calls over a workspace voice channel and registers them
/// with the <see cref="ActiveCallRegistry"/>.
/// </summary>
public sealed class CallPlacementService(
    ICommunicationChannelRegistry channelRegistry,
    ActiveCallRegistry callRegistry)
{
    public async Task<ActiveCallSnapshot> PlaceCallAsync(
        string workspaceKey,
        string? channelId,
        CallTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(target);

        var channel = ResolveChannel(workspaceKey, channelId);
        var call = await channel.PlaceCallAsync(target, cancellationToken).ConfigureAwait(false);
        return callRegistry.TrackPlaced(workspaceKey, channel.ChannelId, call);
    }

    private ICommunicationChannel ResolveChannel(string workspaceKey, string? channelId)
    {
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            if (!channelRegistry.TryGetChannel(workspaceKey, channelId, out var channel) || channel is null)
            {
                throw new InvalidOperationException(
                    $"Channel '{channelId}' is not registered for workspace '{workspaceKey}'.");
            }

            return channel;
        }

        var voiceChannels = channelRegistry.GetChannelsByCapability(workspaceKey, CommunicationCapabilities.Voice);
        return voiceChannels.Count > 0
            ? voiceChannels[0]
            : throw new InvalidOperationException(
                $"No voice channel is registered for workspace '{workspaceKey}'.");
    }
}
